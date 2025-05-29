using BenchmarkingApp.Data;
using BenchmarkingApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace BenchmarkingApp.Controllers
{
    public class BenchmarkController : Controller
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public BenchmarkController(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public IActionResult Index() => View();

        [HttpPost]
        public IActionResult RunCustomTest(List<string> entities, List<string> operations, List<string> executionModes, int iterations)
        {
            var allResults = new List<BenchmarkResult>();

            foreach (var mode in executionModes)
            {
                foreach (var entity in entities)
                {
                    foreach (var operation in operations)
                    {
                        allResults.AddRange(RunBenchmark(entity, operation, mode == "Parallel", iterations));
                    }
                }
            }

            // Adaugă mediile, excluzând "TimpCom"
            var grouped = allResults
                .Where(r => !r.Operation.Contains("TimpCom"))
                .GroupBy(r => new { BaseOp = r.Operation.Split(" - ")[0], r.IsParallel });

            foreach (var group in grouped)
            {
                var avg = group.Average(r => r.ExecutionTimeMs);
                allResults.Add(new BenchmarkResult
                {
                    Operation = $"{group.Key.BaseOp} (Media)",
                    ExecutionTimeMs = (long)avg,
                    IsParallel = group.Key.IsParallel
                });
            }

            return View("Results", allResults);
        }

        private List<BenchmarkResult> RunBenchmark(string entity, string operation, bool isParallel, int iterations)
        {
            var results = new List<BenchmarkResult>();

            for (int i = 0; i < 5; i++) // 5 runde
            {
                var stopwatch = Stopwatch.StartNew();
                using var context = _contextFactory.CreateDbContext();
                long timpCalcul = 0;

                if (isParallel)
                {
                    var individualCalculTimes = new ConcurrentBag<long>();

                    switch (operation)
                    {
                        case "INSERT":
                            Parallel.For(0, iterations, _ =>
                            {
                                var sw = Stopwatch.StartNew();
                                using var ctx = _contextFactory.CreateDbContext();
                                ExecuteInsert(ctx, entity);
                                sw.Stop();
                                individualCalculTimes.Add(sw.ElapsedMilliseconds);
                            });
                            break;

                        case "UPDATE":
                            var itemsToUpdate = GetAllEntities(context, entity).Take(iterations).ToList();
                            Parallel.ForEach(itemsToUpdate, item =>
                            {
                                var sw = Stopwatch.StartNew();
                                using var ctx = _contextFactory.CreateDbContext();
                                ExecuteUpdate(ctx, entity, item);
                                sw.Stop();
                                individualCalculTimes.Add(sw.ElapsedMilliseconds);
                            });
                            break;

                        case "DELETE":
                            var itemsToDelete = GetAllEntities(context, entity).Take(iterations).ToList();
                            Parallel.ForEach(itemsToDelete, item =>
                            {
                                var sw = Stopwatch.StartNew();
                                using var ctx = _contextFactory.CreateDbContext();
                                ExecuteDelete(ctx, entity, item);
                                sw.Stop();
                                individualCalculTimes.Add(sw.ElapsedMilliseconds);
                            });
                            break;

                        case "SELECT":
                            Parallel.For(0, iterations, _ =>
                            {
                                var sw = Stopwatch.StartNew();
                                using var ctx = _contextFactory.CreateDbContext();
                                ExecuteSelect(ctx, entity);
                                sw.Stop();
                                individualCalculTimes.Add(sw.ElapsedMilliseconds);
                            });
                            break;
                    }

                    stopwatch.Stop();
                    timpCalcul = (long)individualCalculTimes.Average();
                    var timpExec = stopwatch.ElapsedMilliseconds;
                    var timpComunicatie = timpExec - timpCalcul;

                    results.Add(new BenchmarkResult
                    {
                        Operation = $"{operation} {entity} - Run {i + 1}",
                        ExecutionTimeMs = timpExec,
                        IsParallel = true
                    });

                    results.Add(new BenchmarkResult
                    {
                        Operation = $"{operation} {entity} - TimpCom Run {i + 1}",
                        ExecutionTimeMs = timpComunicatie,
                        IsParallel = true
                    });
                }
                else
                {
                    switch (operation)
                    {
                        case "INSERT":
                            for (int j = 0; j < iterations; j++)
                                ExecuteInsert(context, entity);
                            break;

                        case "UPDATE":
                            var itemsToUpdate = GetAllEntities(context, entity).Take(iterations).ToList();
                            foreach (var item in itemsToUpdate)
                                ExecuteUpdate(context, entity, item);
                            break;

                        case "DELETE":
                            var itemsToDelete = GetAllEntities(context, entity).Take(iterations).ToList();
                            foreach (var item in itemsToDelete)
                                ExecuteDelete(context, entity, item);
                            break;

                        case "SELECT":
                            for (int j = 0; j < iterations; j++)
                                ExecuteSelect(context, entity);
                            break;
                    }

                    stopwatch.Stop();

                    results.Add(new BenchmarkResult
                    {
                        Operation = $"{operation} {entity} - Run {i + 1}",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        IsParallel = false
                    });
                }
            }

            return results;
        }

        private void ExecuteInsert(AppDbContext context, string entity)
        {
            switch (entity)
            {
                case "Customers":
                    context.Customers.Add(new Customer
                    {
                        Name = $"Customer {Guid.NewGuid()}",
                        Email = $"email{Guid.NewGuid()}@test.com",
                        Phone = "123456789"
                    });
                    break;
                case "Orders":
                    var customer = context.Customers.FirstOrDefault();
                    if (customer != null)
                        context.Orders.Add(new Order
                        {
                            CustomerId = customer.CustomerId,
                            OrderDate = DateTime.Now,
                            TotalAmount = 100
                        });
                    break;
                case "OrderItems":
                    var order = context.Orders.FirstOrDefault();
                    if (order != null)
                        context.OrderItems.Add(new OrderItem
                        {
                            OrderId = order.OrderId,
                            ProductName = $"Product {Guid.NewGuid()}",
                            Quantity = 1,
                            UnitPrice = 10
                        });
                    break;
            }
            context.SaveChanges();
        }

        private void ExecuteUpdate(AppDbContext context, string entity, object obj)
        {
            switch (entity)
            {
                case "Customers":
                    var customer = (Customer)obj;
                    customer.Name = $"Updated {Guid.NewGuid()}";
                    context.Customers.Update(customer);
                    break;
                case "Orders":
                    var order = (Order)obj;
                    order.TotalAmount += 10;
                    context.Orders.Update(order);
                    break;
                case "OrderItems":
                    var item = (OrderItem)obj;
                    item.Quantity++;
                    context.OrderItems.Update(item);
                    break;
            }
            context.SaveChanges();
        }

        private void ExecuteDelete(AppDbContext context, string entity, object obj)
        {
            switch (entity)
            {
                case "Customers":
                    context.Customers.Remove((Customer)obj);
                    break;
                case "Orders":
                    context.Orders.Remove((Order)obj);
                    break;
                case "OrderItems":
                    context.OrderItems.Remove((OrderItem)obj);
                    break;
            }
            context.SaveChanges();
        }

        private void ExecuteSelect(AppDbContext context, string entity)
        {
            switch (entity)
            {
                case "Customers":
                    _ = context.Customers.AsNoTracking().ToList();
                    break;
                case "Orders":
                    _ = context.Orders.AsNoTracking().ToList();
                    break;
                case "OrderItems":
                    _ = context.OrderItems.AsNoTracking().ToList();
                    break;
            }
        }

        private List<object> GetAllEntities(AppDbContext context, string entity)
        {
            return entity switch
            {
                "Customers" => context.Customers.AsNoTracking().Cast<object>().ToList(),
                "Orders" => context.Orders.AsNoTracking().Cast<object>().ToList(),
                "OrderItems" => context.OrderItems.AsNoTracking().Cast<object>().ToList(),
                _ => new List<object>()
            };
        }
    }
}
