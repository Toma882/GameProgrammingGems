/*
 * 快速算法验证脚本
 * 用途：快速验证算法效果，不需要启动WPF可视化
 * 使用：复制到 TestConsole 项目中运行
 *
 * dotnet run --project GPGems.TestConsole
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPGems.AI.Pathfinding;
using GPGems.AI.Boids;
using GPGems.AI.CollisionAvoidance;
using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.TestConsole
{
    class AlgorithmVerification
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Game Programming Gems 算法验证 ===");
            Console.WriteLine();

            // Test 1: 寻路性能Benchmark
            RunPathfindingBenchmark();

            // Test 2: Boids 群体行为
            RunBoidsSimulation();

            // Test 3: ORCA 碰撞避免
            RunORCASimulation();

            Console.WriteLine("\n=== 所有测试完成 ===");
        }

        #region Test 1: 寻路Benchmark
        static void RunPathfindingBenchmark()
        {
            Console.WriteLine("【Test 1】寻路算法性能对比");
            Console.WriteLine("----------------------------------------");

            int mapSize = 100;
            var map = CreateTestGrid(mapSize, mapSize, obstacleDensity: 0.2);

            var aStar = new AStarPathfinder();
            var biAStar = new BidirectionalAStarPathfinder();
            var dijkstra = new DijkstraPathfinder();

            var start = map.GetNode(0, 0);
            var goal = map.GetNode(mapSize - 1, mapSize - 1);

            // A*
            var sw = Stopwatch.StartNew();
            var path1 = aStar.FindPath(map, start, goal);
            sw.Stop();
            Console.WriteLine($"A*          : {sw.ElapsedMilliseconds,4}ms, 路径长度: {path1.Count}, 开放集: {aStar.OpenSetCount}");

            // 双向A*
            sw.Restart();
            var path2 = biAStar.FindPath(map, start, goal);
            sw.Stop();
            Console.WriteLine($"双向A*      : {sw.ElapsedMilliseconds,4}ms, 路径长度: {path2.Count}");

            // Dijkstra
            sw.Restart();
            var path3 = dijkstra.FindPath(map, start, goal);
            sw.Stop();
            Console.WriteLine($"Dijkstra    : {sw.ElapsedMilliseconds,4}ms, 路径长度: {path3.Count}");

            // 流场寻路测试（100个单位）
            Console.WriteLine("\n流场寻路 (100个单位同目标):");
            sw.Restart();
            var flowField = new FlowFieldPathfinder(map);
            flowField.CalculateFlowField(goal.X, goal.Y);
            sw.Stop();
            Console.WriteLine($"  场计算: {sw.ElapsedMilliseconds}ms (一次性)");

            sw.Restart();
            for (int i = 0; i < 100; i++)
            {
                var dir = flowField.GetDirection(i % mapSize, i / mapSize);
            }
            sw.Stop();
            Console.WriteLine($"  100单位查询: {sw.ElapsedMilliseconds}ms (O(1) per单位)");

            Console.WriteLine();
        }
        #endregion

        #region Test 2: Boids 群体行为
        static void RunBoidsSimulation()
        {
            Console.WriteLine("【Test 2】Boids 群体行为模拟 (50单位 × 1000帧)");
            Console.WriteLine("----------------------------------------");

            var flock = new Flock();
            var settings = new BoidSettings();

            // 生成50个Boid
            var rand = new Random(42);
            for (int i = 0; i < 50; i++)
            {
                flock.AddBoid(new Vector3(
                    rand.Next(0, 50),
                    rand.Next(0, 20),
                    rand.Next(0, 50)
                ), Vector3.UnitZ * 2);
            }

            // 设置目标点吸引
            var target = new Vector3(25, 10, 25);
            foreach (var boid in flock.Boids)
            {
                boid.TargetPosition = target;
            }

            // 运行1000帧
            var sw = Stopwatch.StartNew();
            var bounds = new BoundingBox(0, 50, 0, 20, 0, 50);

            for (int frame = 0; frame < 1000; frame++)
            {
                flock.Update(settings, bounds);
            }

            sw.Stop();
            Console.WriteLine($"总耗时: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"平均每帧: {sw.ElapsedMilliseconds / 1000.0:F3}ms");
            Console.WriteLine($"群体最终平均速度: {flock.Boids.Average(b => b.Speed):F2} 单位/秒");

            // 统计凝聚度（到质心的平均距离）
            var center = new Vector3(
                flock.Boids.Average(b => b.Position.X),
                flock.Boids.Average(b => b.Position.Y),
                flock.Boids.Average(b => b.Position.Z)
            );
            float avgDistToCenter = flock.Boids.Average(b => Vector3.Distance(b.Position, center));
            Console.WriteLine($"群体凝聚度: {avgDistToCenter:F2} (越小越聚集)");

            Console.WriteLine();
        }
        #endregion

        #region Test 3: ORCA 碰撞避免
        static void RunORCASimulation()
        {
            Console.WriteLine("【Test 3】ORCA 互惠碰撞避免 (100单位对头碰 × 500帧)");
            Console.WriteLine("----------------------------------------");

            var orca = new ORCASimulation();
            var rand = new Random(42);

            // 左边50个往右走
            for (int i = 0; i < 50; i++)
            {
                var agent = orca.AddAgent(
                    new Vector2(rand.Next(0, 10), rand.Next(0, 50)),
                    radius: 0.5f,
                    maxSpeed: 3f
                );
                agent.Target = new Vector2(100, agent.Position.Y);  // 目标在右边
            }

            // 右边50个往左走
            for (int i = 0; i < 50; i++)
            {
                var agent = orca.AddAgent(
                    new Vector2(rand.Next(90, 100), rand.Next(0, 50)),
                    radius: 0.5f,
                    maxSpeed: 3f
                );
                agent.Target = new Vector2(0, agent.Position.Y);  // 目标在左边
            }

            // 运行500帧
            var sw = Stopwatch.StartNew();
            int collisionCount = 0;

            for (int frame = 0; frame < 500; frame++)
            {
                orca.Update(0.05f);

                // 统计穿透（距离 < 半径之和）
                for (int i = 0; i < orca.Agents.Count; i++)
                {
                    for (int j = i + 1; j < orca.Agents.Count; j++)
                    {
                        float dist = Vector2.Distance(orca.Agents[i].Position, orca.Agents[j].Position);
                        if (dist < 1.0f) collisionCount++;  // 0.5+0.5
                    }
                }
            }

            sw.Stop();
            float collisionPerFrame = collisionCount / 500.0f;

            Console.WriteLine($"总耗时: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"平均每帧: {sw.ElapsedMilliseconds / 500.0:F3}ms");
            Console.WriteLine($"平均每帧碰撞数: {collisionPerFrame:F2} 次 (0.1以下 = 优秀)");
            Console.WriteLine($"穿透率: {collisionPerFrame / (100 * 99 / 2.0):P4}");  // 所有可能对的比例

            if (collisionPerFrame < 1.0)
            {
                Console.WriteLine("✅ ORCA 避障效果：优秀，几乎无穿透");
            }
            else if (collisionPerFrame < 5.0)
            {
                Console.WriteLine("⚠️  ORCA 避障效果：良好，少量轻微穿透");
            }
            else
            {
                Console.WriteLine("❌ ORCA 避障效果：需调参，穿透较多");
            }

            Console.WriteLine();
        }
        #endregion

        #region 辅助方法
        static GridMap CreateTestGrid(int width, int height, double obstacleDensity)
        {
            var map = new GridMap(width, height);
            var rand = new Random(42);  // 固定种子，结果可复现

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (rand.NextDouble() < obstacleDensity)
                    {
                        map.GetNode(x, y).IsWalkable = false;
                    }
                }
            }

            // 确保起点终点可走
            map.GetNode(0, 0).IsWalkable = true;
            map.GetNode(width - 1, height - 1).IsWalkable = true;

            return map;
        }
        #endregion
    }
}
