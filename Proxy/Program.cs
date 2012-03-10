using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections;

namespace Proxy
{
    class Program
    {
        static Regex _extractInfo = new Regex(
            @"^((?:\d+\.){3}\d+):(\d+)$",
            RegexOptions.Singleline | RegexOptions.Compiled);
        //--

        static void Main(string[] args)
        {
            // Validate number of args
            if (args.Length % 3 != 0)
            {
                PrintUsageAndExit();
            }

            List<IProxy> proxies = new List<IProxy>();

            IEnumerator it = args.GetEnumerator();
            while (it.MoveNext())
            {
                string current = it.Current as string;

                if (current == "-u")
                {
                    ReadSrcAndTargetAndQueueIProxy(
                            ref it, 
                            ref proxies, 
                            (src, trg) => { return new UdpProxy(src, trg); });
                    //--
                }
                else if (current == "-t")
                {
                    ReadSrcAndTargetAndQueueIProxy(
                            ref it,
                            ref proxies,
                            (src, trg) => { return new TcpProxy(src, trg); });
                    //--
                }
                else
                {
                    // :S ... Other protocols ? ...
                    Console.WriteLine("ERROR: Invalid protocol option {0}", current);
                    PrintUsageAndExit();
                }
            }
                        
            // Launch proxies
            proxies.ForEach(delegate(IProxy proxy)
            {
                try
                {
                    // Launch proxy ...
                    proxy.LaunchProxy();

                    Console.WriteLine("INFO: Launched proxy {0} from {1} to {2}", 
                            proxy.GetProtocol(), proxy.GetSource(), proxy.GetTarget());
                    //---
                }
                catch (Exception e)
                {
                    Console.WriteLine("EXCEPTION: Error launching one of the proxies ({0}, {1} to {2}): {3}",
                            proxy.GetProtocol(), proxy.GetSource(),
                            proxy.GetTarget(), e.Message);
                    //---
                    PrintUsageAndExit();
                }
            });

            Console.WriteLine("INFO: Press *any key* to end");
            Console.ReadKey();
        }

        static void ReadSrcAndTargetAndQueueIProxy(
                ref IEnumerator it, 
                ref List<IProxy> proxies, 
                Func<IPEndPoint, IPEndPoint, IProxy> generator)
        {
            string ipSource = "";
            int portSource = 0;
            string ipTarget = "";
            int portTarget = 0;
            IPEndPoint source = null, target = null;

            // Extract Source IP and Port
            try
            {
                // TODO: Check if reached end
                it.MoveNext();

                Match sourceMatch = _extractInfo.Match(it.Current as string);

                ipSource = sourceMatch.Groups[1].Value;
                portSource = int.Parse(sourceMatch.Groups[2].Value);
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: Extracting ip and port for source from args {0}: {1}", it.Current, e.Message);
                PrintUsageAndExit();
            }

            // Extract Target IP and Port
            try
            {
                // TODO: Check if reached end
                it.MoveNext();

                Match targetMatch = _extractInfo.Match(it.Current as string);

                ipTarget = targetMatch.Groups[1].Value;
                portTarget = int.Parse(targetMatch.Groups[2].Value);
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: Extracting ip and port for target from args {0}: {1}", it.Current, e.Message);
                PrintUsageAndExit();
            }

            // Create Source EndPoint
            try
            {
                source = new IPEndPoint(IPAddress.Parse(ipSource), portSource);
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: Error generating end point for source with IP {0} and port {1}: {2}", ipSource, portSource, e.Message);
                PrintUsageAndExit();
            }

            // Create Target EndPoint
            try
            {
                target = new IPEndPoint(IPAddress.Parse(ipTarget), portTarget);
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: Error generating end point for target with IP {0} and port {1}: {2}", ipTarget, portTarget, e.Message);
                PrintUsageAndExit();
            }

            // Create and add proxy to list
            
            IProxy proxy = generator(source, target);            
            if (proxy != null)
            {
                proxies.Add(proxy);
            }
        }

        static void PrintUsageAndExit()
        {
            // FIXME: use better syntax ...
            Console.WriteLine("USAGE: Proxy.exe -u|t IPsource:port IPtarget:port ");
            Environment.Exit(0);
        }
    }
}
