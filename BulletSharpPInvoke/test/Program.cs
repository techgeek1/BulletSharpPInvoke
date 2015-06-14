using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BulletSharp;
using BulletSharp.Math;
using BulletSharp.SoftBody;

namespace BulletSharpTest
{
    [StructLayout(LayoutKind.Explicit)]
    struct Vector3WriteTest
    {
        [FieldOffset(0)]
        public float Value1;
        [FieldOffset(4)]
        public Vector3 Vector;
        [FieldOffset(16)]
        public float Value2;
    }

    class Program
    {
        static DiscreteDynamicsWorld world;

        static RigidBody CreateBody(float mass, CollisionShape shape, Vector3 offset)
        {
            var constInfo = new RigidBodyConstructionInfo(mass, new DefaultMotionState(), shape, Vector3.Zero);
            if (mass != 0.0f)
            {
                constInfo.LocalInertia = constInfo.CollisionShape.CalculateLocalInertia(mass);
            }
            var collisionObject = new RigidBody(constInfo);
            collisionObject.Translate(offset);
            world.AddRigidBody(collisionObject);

            AddToDisposeQueue(constInfo);
            AddToDisposeQueue(constInfo.MotionState);
            AddToDisposeQueue(collisionObject);
            AddToDisposeQueue(shape);

            return collisionObject;
        }

        static void TestAlignment()
        {
            const float mass = 1.0f;
            Vector3WriteTest vTest = new Vector3WriteTest();
            vTest.Value1 = 2.0f;
            vTest.Value2 = 3.0f;
            using (BoxShape shape = new BoxShape(1))
            {
                shape.CalculateLocalInertia(mass, out vTest.Vector);
            }
            if (vTest.Value1 != 2.0f || vTest.Value2 != 3.0f)
            {
                Console.WriteLine("Vector3 value was overwritten with padding!");
            }
        }

        static void TestAxisSweepOverlapCallback()
        {
            var conf = new DefaultCollisionConfiguration();
            var dispatcher = new CollisionDispatcher(conf);
            var broadphase = new AxisSweep3(new Vector3(-1000, -1000, -1000), new Vector3(1000, 1000, 1000));
            world = new DiscreteDynamicsWorld(dispatcher, broadphase, null, conf);

            //broadphase.OverlappingPairUserCallback = new AxisSweepUserCallback();
            //AddToDisposeQueue(broadphase.OverlappingPairUserCallback);

            CreateBody(10.0f, new SphereShape(1.0f), new Vector3(2, 2, 0));
            CreateBody(1.0f, new SphereShape(1.0f), new Vector3(0, 2, 0));

            CustomBroadphaseAabbCallback aabbCallback = new CustomBroadphaseAabbCallback();
            broadphase.AabbTest(new Vector3(-1000, -1000, -1000), new Vector3(1000, 1000, 1000), aabbCallback);
            AddToDisposeQueue(aabbCallback);
            aabbCallback = null;

            // FIXME: RayTest crashes for DbvtBroadphase
            CustomBroadphaseRayTestCallback rayCallback = new CustomBroadphaseRayTestCallback();
            //broadphase.RayTest(new Vector3(0, 2, 0), new Vector3(2, 2, 0), rayCallback);
            AddToDisposeQueue(rayCallback);
            rayCallback = null;

            broadphase = null;

            world.StepSimulation(1.0f / 60.0f);

            world.Dispose();
            world = null;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();

            TestWeakRefs();
            disposeQueue.Clear();
        }

        static void TestContactTest(RigidBody testBody, RigidBody testBody2)
        {
            object context = "your context";
            ContactSensorCallback contactCallback = new ContactSensorCallback(testBody, context);
            world.ContactTest(testBody, contactCallback);

            testBody.CollisionFlags |= CollisionFlags.CustomMaterialCallback;
            testBody2.CollisionFlags |= CollisionFlags.CustomMaterialCallback;
            world.ContactPairTest(testBody, testBody2, contactCallback);
            testBody.CollisionFlags &= ~CollisionFlags.CustomMaterialCallback;
            testBody2.CollisionFlags &= ~CollisionFlags.CustomMaterialCallback;

            AddToDisposeQueue(contactCallback);
        }

        static void TestGCCollection()
        {
            var conf = new DefaultCollisionConfiguration();
            var dispatcher = new CollisionDispatcher(conf);
            var broadphase = new DbvtBroadphase();
            //var broadphase = new AxisSweep3(new Vector3(-1000, -1000, -1000), new Vector3(1000, 1000, 1000));
            world = new DiscreteDynamicsWorld(dispatcher, broadphase, null, conf);
            world.Gravity = new Vector3(0, -10, 0);
            dispatcher.NearCallback = DispatcherNearCallback;

            CreateBody(0.0f, new BoxShape(50, 1, 50), Vector3.Zero);
            var dynamicObject = CreateBody(10.0f, new SphereShape(1.0f), new Vector3(2, 2, 0));
            var dynamicObject2 = CreateBody(1.0f, new SphereShape(1.0f), new Vector3(0, 2, 0));

            var ghostPairCallback = new GhostPairCallback();
            broadphase.OverlappingPairCache.SetInternalGhostPairCallback(ghostPairCallback);
            AddToDisposeQueue(ghostPairCallback);
            ghostPairCallback = null;
            var ghostObject = new PairCachingGhostObject();
            ghostObject.CollisionShape = new BoxShape(2);
            ghostObject.WorldTransform = Matrix.Translation(2,2,0);
            world.AddCollisionObject(ghostObject);

            var trimesh = new TriangleMesh();
            Vector3 v0 = new Vector3(0, 0, 0);
            Vector3 v1 = new Vector3(1, 0, 0);
            Vector3 v2 = new Vector3(0, 1, 0);
            Vector3 v3 = new Vector3(1, 1, 0);
            trimesh.AddTriangle(v0, v1, v2);
            trimesh.AddTriangle(v1, v3, v2);
            var triangleMeshShape = new BvhTriangleMeshShape(trimesh, false);
            var triMeshObject = CreateBody(0, triangleMeshShape, new Vector3(20,0,20));
            AddToDisposeQueue(triangleMeshShape);
            AddToDisposeQueue(trimesh);
            AddToDisposeQueue(triMeshObject);
            triangleMeshShape = null;
            trimesh = null;

            AddToDisposeQueue(conf);
            AddToDisposeQueue(dispatcher);
            AddToDisposeQueue(broadphase);
            AddToDisposeQueue(world);

            //conf.Dispose();
            conf = null;
            //dispatcher.Dispose();
            dispatcher = null;
            //broadphase.Dispose();
            broadphase = null;
            world.DebugDrawer = new DebugDrawTest();
            AddToDisposeQueue(world.DebugDrawer);
            world.SetInternalTickCallback(WorldPreTickCallback);
            for (int i = 0; i < 600; i++)
            {
                world.StepSimulation(1.0f / 60.0f);
            }

            world.DispatchInfo.DebugDraw = new DebugDrawTest2();
            AddToDisposeQueue(world.DispatchInfo.DebugDraw);
            world.DispatchInfo.DebugDraw = world.DispatchInfo.DebugDraw;
            AddToDisposeQueue(world.DispatchInfo.DebugDraw);
            world.DispatchInfo.DebugDraw = null;
            world.DebugDrawer = null;
            world.DebugDrawer = new DebugDrawTest2();
            world.StepSimulation(1.0f / 60.0f);
            world.DebugDrawWorld();
            AddToDisposeQueue(world.DispatchInfo.DebugDraw);

            world.DebugDrawer = new DebugDrawTest();
            world.DebugDrawWorld();
            AddToDisposeQueue(world.DebugDrawer);
            world.DebugDrawer = null;

            TestContactTest(dynamicObject, dynamicObject2);
            TestGhostObjectPairs(ghostObject);
            TestRayCast(dynamicObject);
            TestTriangleMeshRayCast(triMeshObject);
            dynamicObject = null;
            dynamicObject2 = null;
            triMeshObject = null;

            //world.SetInternalTickCallback(null);
            world.Dispose();
            world = null;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();

            TestWeakRefs();
            disposeQueue.Clear();
        }

        static void TestGhostObjectPairs(PairCachingGhostObject ghostObject)
        {
            AlignedManifoldArray manifoldArray = new AlignedManifoldArray();
            AlignedBroadphasePairArray pairArray = ghostObject.OverlappingPairCache.OverlappingPairArray;
            int numPairs = pairArray.Count;

            for (int i = 0; i < numPairs; i++)
            {
                manifoldArray.Clear();

                BroadphasePair pair = pairArray[i];

                //unless we manually perform collision detection on this pair, the contacts are in the dynamics world paircache:
                BroadphasePair collisionPair = world.PairCache.FindPair(pair.Proxy0, pair.Proxy1);
                if (collisionPair == null)
                    continue;

                if (collisionPair.Algorithm != null)
                    collisionPair.Algorithm.GetAllContactManifolds(manifoldArray);

                for (int j = 0; j < manifoldArray.Count; j++)
                {
                    PersistentManifold manifold = manifoldArray[j];
                    float directionSign = manifold.Body0 == ghostObject ? -1.0f : 1.0f;
                    for (int p = 0; p < manifold.NumContacts; p++)
                    {
                        ManifoldPoint pt = manifold.GetContactPoint(p);
                        if (pt.Distance < 0.0f)
                        {
                            Vector3 ptA = pt.PositionWorldOnA;
                            Vector3 ptB = pt.PositionWorldOnB;
                            Vector3 normalOnB = pt.NormalWorldOnB;
                            /// work here
                        }
                    }
                }
            }
        }

        static void TestManifoldPoints()
        {
            int numManifolds = world.Dispatcher.NumManifolds;
            for (int i = 0; i < numManifolds; i++)
            {
                PersistentManifold contactManifold = world.Dispatcher.GetManifoldByIndexInternal(i);
                CollisionObject obA = contactManifold.Body0 as CollisionObject;
                CollisionObject obB = contactManifold.Body1 as CollisionObject;

                int numContacts = contactManifold.NumContacts;
                for (int j = 0; j < numContacts; j++)
                {
                    ManifoldPoint pt = contactManifold.GetContactPoint(j);
                    if (pt.Distance < 0.0f)
                    {
                        Vector3 ptA = pt.PositionWorldOnA;
                        Vector3 ptB = pt.PositionWorldOnB;
                        Vector3 normalOnB = pt.NormalWorldOnB;
                    }
                }
            }
        }

        static void TestRayCast(CollisionObject testObject)
        {
            Vector3 rayFromWorld = testObject.WorldTransform.Origin + new Vector3(0, 0, -2);
            Vector3 rayToWorld = testObject.WorldTransform.Origin + new Vector3(0, 0, 2);
            var rayCallback = new CustomRayCallback(ref rayFromWorld, ref rayToWorld);
            world.RayTestRef(ref rayFromWorld, ref rayToWorld, rayCallback);
            if (rayCallback.CollisionObject != testObject)
            {
                Console.WriteLine("Raycast FAILED!");
            }

            AddToDisposeQueue(rayCallback);
        }

        static void TestTriangleMeshRayCast(RigidBody triMeshObject)
        {
            Vector3 rayFromWorld = triMeshObject.WorldTransform.Origin + new Vector3(0, 0, -2);
            Vector3 rayToWorld = triMeshObject.WorldTransform.Origin + new Vector3(0, 0, 2);
            var cb = new TriangleMeshRayCastCallback(ref rayFromWorld, ref rayToWorld);
            world.RayTestRef(ref rayFromWorld, ref rayToWorld, cb);
            if (!cb.Success)
            {
                Console.WriteLine("Triangle mesh raycast fail!");
            }
        }

        static void TestSoftBody()
        {
            var softBodyWorldInfo = new SoftBodyWorldInfo();
            var softBody = new SoftBody(softBodyWorldInfo);
            var softBodyCollisionConf = new SoftBodyRigidBodyCollisionConfiguration();
            var softBodySolver = new DefaultSoftBodySolver();
            var dispatcher = new CollisionDispatcher(softBodyCollisionConf);
            var broadphase = new AxisSweep3(new Vector3(-1000, -1000, -1000),
                new Vector3(1000, 1000, 1000));
            var softBodyWorld = new SoftRigidDynamicsWorld(dispatcher, broadphase, null, softBodyCollisionConf, softBodySolver);
            softBodyWorld.AddSoftBody(softBody);

            if (!object.ReferenceEquals(softBody.SoftBodySolver, softBodySolver))
            {
                Console.WriteLine("SoftBody: body and world SoftBodySolvers don't match!");
            }
            
            AddToDisposeQueue(softBodyWorldInfo);
            AddToDisposeQueue(softBody);
            AddToDisposeQueue(softBodyCollisionConf);
            AddToDisposeQueue(softBodySolver);
            AddToDisposeQueue(dispatcher);
            AddToDisposeQueue(broadphase);
            AddToDisposeQueue(softBodyWorld);

            softBodyWorldInfo = null;
            softBody = null;
            softBodyCollisionConf = null;
            softBodySolver = null;
            dispatcher = null;
            broadphase = null;
            softBodyWorld.Dispose();
            softBodyWorld = null;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();

            TestWeakRefs();
            disposeQueue.Clear();
        }

        static void TestWeakRefs()
        {
            foreach (var r in disposeQueue)
            {
                TestWeakRef(r.Key, r.Value);
            }
        }

        static void TestWeakRef(string name, WeakReference wr)
        {
            if (wr.IsAlive)
            {
                Console.Write(name + " GC collection FAILED! ");
                Console.WriteLine("Gen: " + GC.GetGeneration(wr.Target));
            }
            else
            {
                //Console.WriteLine(name + " GC collection OK");
            }
        }

        static Dictionary<string, WeakReference> disposeQueue = new Dictionary<string, WeakReference>();
        static void AddToDisposeQueue(object obj)
        {
            var r = new WeakReference(obj);
            string name = obj.GetType().Name;
            if (disposeQueue.ContainsKey(name))
            {
                int i = 2;
                var name2 = name + i.ToString();
                while (disposeQueue.ContainsKey(name2))
                {
                    i++;
                    name2 = name + i.ToString();
                }
                name = name2;
            }
            disposeQueue.Add(name, r);
        }

        static void WorldPreTickCallback(DynamicsWorld world2, float timeStep)
        {
            TestManifoldPoints();

            //Console.WriteLine("WorldPreTickCallback");
            if (!object.ReferenceEquals(world, world2))
            {
                Console.WriteLine("WorldPreTickCallback: World reference lost!");
            }
        }

        static void DispatcherNearCallback(BroadphasePair collisionPair, CollisionDispatcher dispatcher,
			DispatcherInfo dispatchInfo)
        {
            //AddToDisposeQueue(dispatchInfo.DebugDraw);
            TestManifoldPoints();
            //Console.WriteLine("DispatcherNearCallback");
        }

        static void Main(string[] args)
        {
            TestAlignment();
            TestAxisSweepOverlapCallback();
            TestGCCollection();
            TestSoftBody();

            Console.WriteLine("Finished");
            Console.ReadKey();
        }
    }

    class CustomRayCallback : ClosestRayResultCallback
    {
        public CustomRayCallback(ref Vector3 rayFrom, ref Vector3 rayTo)
            : base(ref rayFrom, ref rayTo)
        {
        }

        public override float AddSingleResult(LocalRayResult rayResult, bool normalInWorldSpace)
        {
            return base.AddSingleResult(rayResult, normalInWorldSpace);
        }
    }
    /*
    class AxisSweepUserCallback : OverlappingPairCallback
    {
        public AxisSweepUserCallback()
        {
        }

        public override BroadphasePair AddOverlappingPair(BroadphaseProxy proxy0, BroadphaseProxy proxy1)
        {
            return null;
        }

        public override IntPtr RemoveOverlappingPair(BroadphaseProxy proxy0, BroadphaseProxy proxy1, Dispatcher dispatcher)
        {
            return IntPtr.Zero;
        }

        public override void RemoveOverlappingPairsContainingProxy(BroadphaseProxy proxy0, Dispatcher dispatcher)
        {
            throw new NotImplementedException();
        }
    }
    */
    class CustomBroadphaseAabbCallback : BroadphaseAabbCallback
    {
        public override bool Process(BroadphaseProxy proxy)
        {
            return true;
        }
    }

    class CustomBroadphaseRayTestCallback : BroadphaseRayCallback
    {
        public override bool Process(BroadphaseProxy proxy)
        {
            return true;
        }
    }

    public class TriangleMeshRayCastCallback : ClosestRayResultCallback
    {
        public int TriangleIndex { get; private set; }
        public bool Success { get; private set; }

        public TriangleMeshRayCastCallback(ref Vector3 from, ref Vector3 to)
            : base(ref from, ref to)
        {
        }

        public override float AddSingleResult(LocalRayResult rayResult, bool normalInWorldSpace)
        {
            if (rayResult.LocalShapeInfo != null)
            {
                Success = true;
                TriangleIndex = rayResult.LocalShapeInfo.TriangleIndex;
            }
            return base.AddSingleResult(rayResult, normalInWorldSpace);
        }
    }
}
