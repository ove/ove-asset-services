using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OVE.Service.NetworkTiles.QuadTree.Tree.Utilities;

namespace OVE.Service.NetworkTiles.QuadTree.Tree {
    /// <summary>
    ///  this is a abstract factory design pattern (its also generic)
    /// </summary>
    /// <typeparam name="T">the type of thing within the quadtree</typeparam>
    public abstract class AConcurrentQuadTreeFactory<T> where T : IQuadable<double> {
        protected readonly ILogger Logger;

        protected int MaxBags;
        public int MaxObjectsPerBag;
        protected int MaxWorklistSize;
        protected int Delay;
        public QuadTree<T> QuadTree { get; protected set; }

        protected readonly ConcurrentQueue<Tuple<string, List<T>>> WorkList =
            new ConcurrentQueue<Tuple<string, List<T>>>();

        protected readonly ConcurrentDictionary<string, QuadTreeNode<T>> Quads =
            new ConcurrentDictionary<string, QuadTreeNode<T>>();

        //private ConcurrentQueue<string> _logMessages = new ConcurrentQueue<string>();

        private int _addTasks;
        private int _workTasks;
        private int _reworkTasks;

        private int TotalThreads => _workTasks + _addTasks + _reworkTasks;

        private SemaphoreSlim _completionSemaphore;
        private SemaphoreSlim _adderCompletionSemaphore;
        private SemaphoreSlim _workerCompletionSemaphore;
        private SemaphoreSlim _reworkCompletionSemaphore;

        /// <summary>
        /// a semaphore to prevent multiple concurrent use of this class
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global - this will be needed by implementors
        protected readonly SemaphoreSlim CurrentlyAdding = new SemaphoreSlim(1, 1);

        protected AConcurrentQuadTreeFactory(ILogger logger) {
            Logger = logger;
        }

        /// <summary>
        /// Removes a bag of objects from Quadtree c# memory to remote storage
        /// </summary>
        /// <param name="obj">The object.</param>
        protected abstract void ShedObjects(QuadTreeBag<T> obj);

        /// <summary>
        /// Registers the quad in a database of guids
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <param name="quad">The quad.</param>
        protected abstract void RegisterQuad(string guid, QuadTreeNode<T> quad);

        /// <summary>
        /// Marks the bags of objects pushed from one quadtreeNode for rework.
        /// This happens when a leaf becomes a node 
        /// </summary>
        /// <param name="quadGuid">The quad unique identifier.</param>
        protected abstract void MarkObjectsForRework(string quadGuid);

        /// <summary>
        /// Concurrently adds objects - using as many threads as there are items in the list
        /// </summary>
        /// <param name="addObjects">The add objects. we group objects into List(T)s we have an unknown number of these groups and many threads and provide</param>
        /// <param name="workThreads">The number of worker threads.</param>
        /// <param name="reworkThreads">The number of rework threads.</param>
        /// <returns>if it succeeded</returns>
        public bool ConcurrentAdd(List<IEnumerable<List<T>>> addObjects, int workThreads = 1, int reworkThreads = 1) {
            Log("---- ConcurrentAdd ----");
            // check nothing else is running
            if (!CurrentlyAdding.WaitAsync(1000).Result) return false;

            SetupCountersAndSemaphores(addObjects.Count, workThreads, reworkThreads);
            List<Task> addTasks = StartAddTasks(addObjects);
            List<Task> workerTasks = StartWorkerTasks(_workTasks);
            List<Task> reWorkerTasks = StartReWorkerTasks(_reworkTasks);
            List<Task> allTasks = addTasks.Concat(workerTasks).Concat(reWorkerTasks).ToList();
            Task continuation = Task.WhenAll(allTasks);

            continuation.Wait();

            // double check that we're finished
            for (int i = 0; i < TotalThreads; i++) {
                _completionSemaphore.Wait();
            }

            // allow others to use the class
            CurrentlyAdding.Release();
            return continuation.Status == TaskStatus.RanToCompletion;
        }

        private void SetupCountersAndSemaphores(int addThreads, int workThreads, int reworkThreads) {
            this._addTasks = addThreads;
            this._workTasks = workThreads;
            this._reworkTasks = reworkThreads;
            this._completionSemaphore = new SemaphoreSlim(0, TotalThreads);
            this._adderCompletionSemaphore = new SemaphoreSlim(0, _addTasks);
            this._workerCompletionSemaphore = new SemaphoreSlim(0, _workTasks);
            this._reworkCompletionSemaphore = new SemaphoreSlim(0, _reworkTasks);
        }

        // Number of threads, groups of objects per thread, objects given in batches
        private List<Task> StartAddTasks(IEnumerable<IEnumerable<List<T>>> addObjects) {
            Log("Starting Add tasks");
            List<Task> tasks = addObjects.Select(listOfWork => Task.Run(async () => {
                    Log("Started adder Thread");
                    foreach (List<T> o in listOfWork) {
                        while (WorkList.Count > MaxWorklistSize) {
                            Log("Worklist full - adder sleeping");
                            await Task.Delay(Delay).ConfigureAwait(false);
                            // note that this will not stop adding items, just slow down the rate of addition to the worklist
                        }
                        // add objects
                        WorkList.Enqueue(new Tuple<string, List<T>>(QuadTree.Root.Guid, o));
                    }

                    Log("Finished adder Thread");
                    this._adderCompletionSemaphore.Release();
                    this._completionSemaphore.Release();
                }))
                .ToList();

            Log("Finished creating Add tasks");
            return tasks;
        }
        
        private List<Task> StartWorkerTasks(int workTasks) {
            Log("Starting Worker tasks");
            List<Task> tasks = new List<Task>();
            for (int workerId = 0; workerId < workTasks; workerId++) {
                tasks.Add(Task.Run(async () => {

                    try {
                        Log("Started worker Thread");
                        // Stop criteria: All the objects were pushed by the adder thread, and there is no Rework and the worklist is empty 
                        while (this._adderCompletionSemaphore.CurrentCount != _addTasks || !ReworkQueueEmpty() ||
                               !WorkList.IsEmpty) {
                            // try and get some work
                            while (WorkList.TryDequeue(out var workItem)) {
                                // this second loop is an optimization 
                                // get the quadtree workItem.Item1 by its id
                                try {
                                    QuadTreeNode<T> quad = Quads[workItem.Item1];
                                    // if this throws we have a big problem 

                                    foreach (var o in workItem.Item2) {
                                        QuadTree.AddObject(quad, o);
                                    }
                                }
                                catch (Exception e) {
                                    Logger.LogError(e,"unable to add obj to tree");
                                }
                            }

                            Log("no work for worker,sleeping, _adderCompletionSemaphore.CurrentCount: " +
                                _adderCompletionSemaphore.CurrentCount + ", ReworkQueueEmpty(): " + ReworkQueueEmpty() +
                                ", _reworkCompletionSemaphore.CurrentCount: " +
                                _reworkCompletionSemaphore.CurrentCount);
                            await Task.Delay(Delay).ConfigureAwait(false); // we have no context to return to 
                            Log("no work for worker,awake");
                        }

                        Log("Finished worker  Thread");
                    }
                    catch (Exception e) {
                        Logger.LogError(e,"error in worker code");
                    }

                    this._workerCompletionSemaphore.Release();
                    this._completionSemaphore.Release();
                }));
            }

            Log("Finished creating worker tasks");
            return tasks;
        }

        private List<Task> StartReWorkerTasks(int reworkTasks) {
            Log("Starting reWorker tasks");
            List<Task> tasks = new List<Task>();

            for (int workerId = 0; workerId < reworkTasks; workerId++) {
                tasks.Add(
                    Task.Run(async () => {
                        Log("Started reWorker Thread");
                        // Stop criteria: Adder pushed all its objects, and there is no object within the Mongo to repush inside the worklist, and No object flagged within the Mongo
                        //while (_adderCompletionSemaphore.CurrentCount != _addtasks || !Worklist.IsEmpty || !ReworkQueueEmpty())

                        try {
                            while (!WorkList.IsEmpty || !ReworkQueueEmpty() ||
                                   _adderCompletionSemaphore.CurrentCount != _addTasks ||
                                   _workerCompletionSemaphore.CurrentCount != _workTasks) {

                                // try get a bag to work with
                                bool foundObj = true;
                                // while the worklist isn't full and we can still find objs
                                while (WorkList.Count <= MaxWorklistSize && foundObj) {
                                    if (GetReworkBag(out var bag, QuadTree.TreeId)) {
                                        WorkList.Enqueue(new Tuple<string, List<T>>(bag.QuadId, bag.Objects));
                                        Log("Found a bag to rework");
                                    }
                                    else {
                                        foundObj = false;
                                        Log("Found no bags to rework, sleeping");
                                    }
                                }

                                if (WorkList.Count >= MaxWorklistSize) {
                                    Log("worklist full reworker going to sleep");
                                }

                                await Task.Delay(Delay).ConfigureAwait(false); // we have no context to return to 
                            }

                            if (!ReworkQueueEmpty()) {
                                throw new Exception("failed, at the end of the reworker loop, ReworkQueueEmpty is false!");
                            }

                            Log("Finished reworker Thread");
                            this._reworkCompletionSemaphore.Release();
                            this._completionSemaphore.Release();

                        }
                        catch (Exception e) {
                            Logger.LogError(e,"failed reworker");
                            throw;
                        }
                    }));
            }

            Log("Finished creating reworker tasks");
            return tasks;
        }
        
        protected abstract bool ReworkQueueEmpty();
    
        public abstract bool GetReworkBag(out QuadTreeBag<T> bag);

        public abstract bool GetReworkBag(out QuadTreeBag<T> bag, string treeId);

        private void Log(string msg) {
            Logger.LogInformation(DateTime.Now + " [" + Task.CurrentId + "]" + msg);
        }
        
        /// <summary>
        /// Determines whether the factory has terminated with clean state - e.g. worklist and rework list are empty 
        /// </summary>
        /// <returns></returns>
        public abstract bool HasCleanState();

        public abstract int TotalObjectsInStorage();

        public abstract string PrintShedBags();

        public virtual string PrintState() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"adders {this._adderCompletionSemaphore.CurrentCount}/{_addTasks} finished");
            sb.AppendLine($"workers {this._workerCompletionSemaphore.CurrentCount}/{_workTasks} finished");
            sb.AppendLine($"reworkers {this._reworkCompletionSemaphore.CurrentCount}/{_reworkTasks} finished");
            sb.AppendLine($"worklist = {this.WorkList.Count}");
            sb.AppendLine();
            return sb.ToString();

        }

        public string PrintQuad() {
            return this.QuadTree.PrintQuad(this);
        }

        public abstract QuadTreeBag<T>[] GetBagsForQuad(QuadTreeNode<T> quad);
        public abstract Dictionary<string, QuadTreeNode<T>> SelectLeafs();
    }
}