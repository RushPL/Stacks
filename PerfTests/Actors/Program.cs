﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using System.Management;
using System.Threading;
using System.Diagnostics;

namespace Actors
{
    class Program
    {
        public static uint CPUSpeed()
        {
            ManagementObject mo = new ManagementObject("Win32_Processor.DeviceID='CPU0'");
            uint sp = (uint)(mo["CurrentClockSpeed"]);
            mo.Dispose();
            return sp;
        }

        static void Main(string[] args)
        {
            int workerThreads;
            int completionPortThreads;
            ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);

            Console.WriteLine("Worker threads: {0}", workerThreads);
            Console.WriteLine("OSVersion: {0}", Environment.OSVersion);
            Console.WriteLine("ProcessorCount: {0}", Environment.ProcessorCount);
            Console.WriteLine("ClockSpeed: {0} MHZ", CPUSpeed());

            Console.WriteLine("Actor count, Messages/sec");

            Benchmark(8);
            
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Done..");
            Console.ReadKey();
        }
      
        private static int redCount = 0;
        private static long bestThroughput = 0;
      
        private static bool Benchmark(int numberOfClients)
        {
            var repeatFactor = 500;
            var repeat = 30000L * repeatFactor;
            var repeatsPerClient = repeat / numberOfClients;
            //var system = new ActorSystem("PingPong");

            var clients = new List<Client>();
            var tasks = new List<Task>();
            for (int i = 0; i < numberOfClients; i++)
            {
                var destination = new Destination();
                var ts = new TaskCompletionSource<bool>();
                tasks.Add(ts.Task);
                var client = new Client(destination, repeatsPerClient, ts);
                clients.Add(client);
            }

            clients.ForEach(c => c.Start());

            var sw = Stopwatch.StartNew();
            Task.WaitAll(tasks.ToArray());
            sw.Stop();
            var totalMessagesReceived = repeat * 2; //times 2 since the client and the destination both send messages
            
            long throughput = totalMessagesReceived / sw.ElapsedMilliseconds * 1000;
            if (throughput > bestThroughput)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                bestThroughput = throughput;
                redCount = 0;
            }
            else
            {
                redCount++;
                Console.ForegroundColor = ConsoleColor.Red;
            }

            Console.WriteLine("{0}, {1} messages/s", numberOfClients * 2, throughput);

            if (redCount > 3)
                return false;

            return true;
        }

        private static void WaitForEmptyThreadPool()
        {
            int count = 100;
            var tasks = new Task[count];
            for (int i = 0; i < count; i++)
            {
                tasks[i] = Task.Factory.StartNew(() => { });
            }

            Task.WaitAll(tasks);
        }

        public class Destination : Actor
        {
            public async void Ping(Client c)
            {
                await Context;

                c.Pong();
            }
        }

        public class Client : Actor
        {
            public long received;
            public long sent;

            public long repeat;
            private Destination actor;
            private TaskCompletionSource<bool> latch;

            public Client(Destination actor, long repeat, TaskCompletionSource<bool> latch)
            {
                this.actor = actor;
                this.repeat = repeat;
                this.latch = latch;
            }

            public async void Pong()
            {
                await Context;

                ++received;
                if (sent < repeat)
                {
                    actor.Ping(this);
                    sent++;
                }
                else if (received >= repeat)
                {
                    latch.SetResult(true);
                }
            }

            public async void Start()
            {
                await Context;

                for (int i = 0; i < Math.Min(1000, repeat); i++)
                {
                    actor.Ping(this);
                    sent++;
                }
            }
        }
    }
}
