using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Utilities;

namespace OVE.Service.NetworkTiles.QuadTree.Domain {
    /// <summary>
    /// This class was originally developed by David Chai for his Msc Project supervised by Dr David Birch at the Data Science Institute Imperial College
    /// It was subsequently collaboratively refactored by Dr Miguel Molina and Dr David Birch for inclusion in the GDO framework github.com/bkavuncu/GDO (MIT License)
    /// It was then refactored by David Birch and Kevin Allain for use within the Map of the Universe project at the Institute
    /// It has been heavily refactored for inclusion in OVE by David Birch
    /// 
    /// TODO this class reads files in two ways
    /// 1) streaming graph readers
    /// 2) read everything
    /// 
    /// mostly we should use #2 because it does the graph normalisation and gives links their start and finish coordinates
    /// however this is extremely memory intensive for large graphs :-/ 
    /// the solution is to provide the scaling factors outside of the graph reader and to provide the links with their start and finish coordinates...
    /// </summary>
    public class GraphmlReader {
        private ILogger _logger;

        public GraphInfo GraphInfo { get; set; }
        public Dictionary<string, GraphNode> NodesById;
        public List<GraphLink> Edges;

        // default sizes for nodes and links
        private const float DefaultNodeSize = 1;
        private const float DefaultLinkWidth = 1;
        private readonly Color _defaultNodeColor = Color.Gray;
        private readonly Color _defaultEdgeColor = Color.Gray;

        private readonly float _objectiveWidth = 64; //todo this needs to be changed
        private readonly float _objectiveHeight = 9;

        private float _yScale;
        private float _xScale;

        private readonly string[] _mandatoryNodeKeys = {"x", "y", "r", "g", "b", "size"};
        private List<KeyField> _keys;
        private List<KeyField> _nodeKeys;
        private bool _hasLabel;
        private bool _hasSize;
        private bool _nodeHasR;
        private bool _nodeHasG;
        private bool _nodeHasB;
        private bool _hasX;
        private bool _hasY;
        private bool _nodeHasColour;
        private bool _nodeHasPosition;
        private List<KeyField> _edgeKeys;
        private bool _edgeHasWeight;
        private bool _edgeHasR;
        private bool _edgeHasG;
        private bool _edgeHasB;
        private bool _edgeHasColour;
        private float _minX;
        private float _minY;
        private float _maxX;
        private float _maxY;

        public GraphmlReader(ILogger logger) {
            this._logger = logger;
        }

        private static object TryParseFloat(string data) => float.TryParse(data, out float f) ? (object) f : data;

        /// <summary>
        /// Reads the graph ml data.
        /// </summary>
        /// <param name="graphmlFile">The graphml file.</param>
        /// <param name="cacheNodesForEdgeLookup">if set to <c>true</c> [cache nodes for edge lookup]. this is EXTREMELY Memory intensive</param>
        /// <returns></returns>
        public GraphInfo ReadGraphmlData(string graphmlFile, bool cacheNodesForEdgeLookup = true) {
            this.GraphInfo = new GraphInfo();

            Stopwatch sw = new Stopwatch();
            sw.Start();

            XNamespace ns = @"http://graphml.graphdrawing.org/xmlns";

            string meta = "";
            foreach (var l in File.ReadLines(graphmlFile)) {
                meta += l + Environment.NewLine;
                if (l.Contains("</node>")) {
                    break;
                }
            }

            meta += "</graph></graphml>";

            var xReader = XmlReader.Create(new StringReader(meta));
            XDocument doc = XDocument.Load(xReader);

            _keys = doc.Root.Elements(ns + "key").Select(k =>
                new KeyField {
                    Id = k.Attribute("id").Value, Name = k.Attribute("attr.name").Value,
                    ValueType = k.Attribute("attr.type").Value, AppliesTo = k.Attribute("for").Value
                }).ToList();

            ComputeGraphProperties();
            
            GraphInfo.NodeMandatoryFields =
                _nodeKeys.Where(f => _mandatoryNodeKeys.Contains(f.Name)).Select(a => a.Name).ToList();
            GraphInfo.NodeOtherFields =
                _nodeKeys.Where(f => !_mandatoryNodeKeys.Contains(f.Name)).Select(a => a.Name).ToList();
            GraphInfo.LinkKeys = _edgeKeys.Select(f => f.Name).ToList();

            var nodesById = new Dictionary<string, GraphNode>();
            if (cacheNodesForEdgeLookup) {
                this.NodesById = nodesById;
            }

            foreach (XElement singleNode in XmlExtensions.StreamReadXElement(graphmlFile, "node")) {
                var data = singleNode.Elements(ns + "data").ToDictionary(
                    xdata => xdata.Attribute("key").Value,
                    xdata => xdata.Value);

                #region fill Nodes data structure

                GraphNode newNode = new GraphNode {
                    Id = singleNode.Attribute("id").Value,
                    // remove attrs that have their own field (R,G,B, size)
                    Label = _hasLabel && data.ContainsKey("label") ? data["label"] : "",
                    // leave labels blank
                    Attrs = data.Where(a => !_mandatoryNodeKeys.Contains(a.Key))
                        .ToDictionary(a => a.Key, a => TryParseFloat(a.Value)),
                    Pos = new Position {
                        X = float.Parse(data["x"]),
                        Y = float.Parse(data["y"]),
                    },
                    R = _nodeHasColour ? int.Parse(data["r"]) : _defaultNodeColor.R,
                    G = _nodeHasColour ? int.Parse(data["g"]) : _defaultNodeColor.G,
                    B = _nodeHasColour ? int.Parse(data["b"]) : _defaultNodeColor.B,
                    SizeF = _hasSize ? float.Parse(data["size"]) : DefaultNodeSize,
                    Adj = new List<string>(),
                    NumLinks = 0
                };

                nodesById.Add(newNode.Id, newNode);
            }

            #endregion

            #region rescale nodes

            this._minX = nodesById.Min(n => n.Value.Pos.X);
            this._minY = nodesById.Min(n => n.Value.Pos.Y);
            this._maxX = nodesById.Max(n => n.Value.Pos.X);
            this._maxY = nodesById.Max(n => n.Value.Pos.Y);
            _logger.LogInformation("x = " + this._minX + " > " + this._maxX);
            _logger.LogInformation("y = " + this._minY + " > " + this._maxY);

            GraphInfo.RectDim = new RectDimension {
                Width = _maxX - _minX,
                Height = _maxY - _minY
            };

            this._xScale = this._objectiveWidth / this.GraphInfo.RectDim.Width; // 16*16  > 16*2
            this._yScale = this._objectiveHeight / this.GraphInfo.RectDim.Height; // 9*4

            _logger.LogInformation("x Scale = " + this._xScale);
            _logger.LogInformation("y Scale = " + this._yScale);

            //rescale nodes...
            foreach (var node in nodesById.Values) {
                node.Pos.X = (node.Pos.X - _minX) * this._xScale;
                node.Pos.Y = (node.Pos.Y - _minY) * this._yScale;
            }

            var newXMin = nodesById.Min(n => n.Value.Pos.X);
            var newYMin = nodesById.Min(n => n.Value.Pos.Y);
            var newXMax = nodesById.Max(n => n.Value.Pos.X);
            var newYMax = nodesById.Max(n => n.Value.Pos.Y);

            GraphInfo.RectDim = new RectDimension {
                Width = newXMax - newXMin,
                Height = newYMax - newYMin
            };
            _logger.LogInformation("writing new graph");

            #endregion

            #region fill Links data structure 

            this.Edges = new List<GraphLink>();
            foreach (XElement xn in XmlExtensions.StreamReadXElement(graphmlFile, "edge")) {
                var l =
                    new {
                        source = xn.Attribute("source").Value,
                        target = xn.Attribute("target").Value,
                        data = xn.Elements(ns + "data").Any()
                            ? xn.Elements(ns + "data").ToDictionary(xdata => xdata.Attribute("key").Value,
                                xdata => xdata.Value)
                            : new Dictionary<string, string>()
                    };
                var link = new GraphLink {
                    Source = l.source,
                    Target = l.target,
                    Attrs = l.data.ToDictionary(a => a.Key, a => TryParseFloat(a.Value)),
                    Weight = _edgeHasWeight ? float.Parse(l.data["weight"]) : DefaultLinkWidth,
                    R = _edgeHasColour ? int.Parse(l.data["r"]) : _defaultEdgeColor.R,
                    G = _edgeHasColour ? int.Parse(l.data["g"]) : _defaultEdgeColor.G,
                    B = _edgeHasColour ? int.Parse(l.data["b"]) : _defaultEdgeColor.B,
                    StartPos = nodesById[l.source].Pos,
                    EndPos = nodesById[l.target].Pos
                };
                this.Edges.Add(link);
            }

            #endregion

            sw.Stop();
            _logger.LogInformation("Time to read the Graphml file: " + sw.ElapsedMilliseconds + "ms");

            return GraphInfo;
        }

        private void ComputeGraphProperties() {
            this._nodeKeys = _keys.Where(k => k.AppliesTo == "node").ToList();

            this._hasLabel = _nodeKeys.Any(k => k.Name == "label");
            this._hasSize = _nodeKeys.Any(k => k.Name == "size");
            this._nodeHasR = _nodeKeys.Any(k => k.Name == "r");
            this._nodeHasG = _nodeKeys.Any(k => k.Name == "g");
            this._nodeHasB = _nodeKeys.Any(k => k.Name == "b");
            this._hasX = _nodeKeys.Any(k => k.Name == "x");
            this._hasY = _nodeKeys.Any(k => k.Name == "y");

            this._nodeHasColour = _nodeHasR && _nodeHasG && _nodeHasB;
            this._nodeHasPosition = _hasX && _hasY; //TODO throw exception / log

            this._edgeKeys = _keys.Where(k => k.AppliesTo == "edge").ToList();
            this._edgeHasWeight = _edgeKeys.Any(k => k.Name == "weight");
            this._edgeHasR = _edgeKeys.Any(k => k.Name == "r");
            this._edgeHasG = _edgeKeys.Any(k => k.Name == "g");
            this._edgeHasB = _edgeKeys.Any(k => k.Name == "b");
            this._edgeHasColour = _edgeHasR && _edgeHasG && _edgeHasB;
        }
        
        /// <summary>
        /// this groups the read objects into groups of objects 
        /// </summary>
        /// <param name="graphObj">The graph objects </param>
        /// <returns></returns>
        public static IEnumerable<List<TK>> BatchObjects<T, TK>(IEnumerable<T> graphObj) where T : TK {
            var enumerator = graphObj.GetEnumerator();
            List<TK> list = new List<TK>();
            int count = 0;
            while (enumerator.MoveNext()) {

                list.Add(enumerator.Current);
                count++;
                if (count >= 500) {
                    yield return list;
                    list = new List<TK>();
                    count = 0;
                }
            }

            if (list.Any()) {
                yield return list;
            }

            enumerator.Dispose();
        }
    }
}