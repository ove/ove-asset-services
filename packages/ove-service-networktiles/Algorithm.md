# Network Tile Service Algorithm

This goal of this document is to provide a high level understanding of the functionality and algorithm implemented by this asset processing service. 

## Context

 * Large network graphs are often too large for a single renderer because:
    * Rendering may take a long time, or even crash due to running out of memory. 
    * The amount of detail that can be shown is limited by the number of pixels on the display.

 * Large High Resolution Display (LHRD) environments address these limitations by:
    * Providing a much large pixel count, allowing finer detail to be shown.  
    * Splitting the rendering process across multiple rendering nodes. 
 * This necessitates sharding the network graph into smaller pieces so that each render need only retrieve and render the network data for its part of the whole screen. 
 * Even with sharding, some datasets will still exceed the limitations of the renderers collectively. In this case a process of down-sampling or sparsification must be employed. 
 * Note that this problem is subtly different from rendering a very large image from a set of image tiles, such as those provided by the Deepzoom format. This is since:
    * The volume of image data taken to "fill" a screen is always constant, even when at a higher zoom level. 
    * The amount of network graph data shown on an individual screen is highly non-uniform and may exceed what can be rendered in a given number of pixels. 
    * Data is not discretely shardable: an edge may span across a sharding boundary and need to be represented in both shards.
    

## Objectives

In conjunction with a distributed graph renderer, the Network Tile Service will provide sharded access to network graph data, and enable interactive zoom and pan through the graph network.

**Implementation**:
The service will decouple processing tasks - reading data, creating a quadtree, exporting data - to take advantage of concurrent processing and enable processing of data larger than would fit within system main memory (e.g. from multiple databases).

**Extensions**:
* To provide a sparsification service to enable much larger graphs to be viewed.
* To provide a consistent view of the data - if part of an edge is shown then the whole edge should be shown. 


## Approach & Algorithm

A Quadtree is used to provide recursive sharding of the network. This sharding is done by recursive bifurcation in both dimensions to give 4 children per node. Objects may only resider at the leaves of the quad tree structure.  

The Quadtree is built concurrently using three different types of thread (there may be multiple instances of each). Initially the Quadtree consists of a single node and it is expanded raggedly as required.  

* **Adder** threads are responsible for acquiring data (from file or from multiple databases) to be added to the quadtree, parsing it and adding it to a worklist for the **Worker** threads.
* **Worker** threads are responsible for adding objects to the quadtree starting from a particular node (normally the root). When a leaf in the quadtree becomes full of objects 4 new nodes are generated as its children. The objects currently residing in the node are marked for Rework.
* **Reworkers** threads are responsible for taking objects marked for rework, storing them in temporary storage, and then adding them to the worklist for workers to undertake.

Throughout the quad tree objects are stored in groups of **Bags**; each quad tree node may contain multiple bags. To avoid the quadtree filling main memory Bags may be **Shed** to external storage - particularly when objects are marked for rework as above. 

Once the Quadtree is generated one JSON file is generated per leaf consisting of all of the objects within the bags attached to that leaf. Object locations (X and Y) are normalised before writing. This process is done in parallel. 

The Quadtree data structure itself then acts as an index file to these leaf files and is written to a file. The index is cached in memory for future lookups O(Log4 n). 

## Rendering

 * When a graph is loaded onto a LHRD, information about capabilities of the rendering cluster (specifically the number of renders and how many bags of elements they can smoothly render) are passed to the NetworkTile service along with the Asset representing the graph. The service will start reading and caching the quad tree index file. 
 * In return information about the dimensions and aspect ratio of the graph are provided. These should be shared with each renderer. 
 * Each render will then use this information and knowledge of their location in the cluster to request a particular rectangle of the graph from the Network Tile Service.
 * The Network Tile Service will provide a list of URLs of JSON files holding the graph objects (nodes and edges) within that rectangle of the graph (subject to the constraints passed earlier). Alongside this list will be information about the rectangle which each JSON file covers. This should be used to create a renderer on the screen to render that single file. 

## Sparsification 

Given the non-uniform spatial density of the network it is necessary to employ mechanisms to avoid overloading individual renderers. This is achieved by a depth first algorithm which proportionally samples the graph elements in the 4 leaves of node and creates a sparse sampling of them which is stored in the node. This process is done at each level up to the top of the the quad tree node. 
