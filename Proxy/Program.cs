using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;

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

            // Parse args and build proxies to launch
            for (int i = 0; i < args.Length; i += 3)
            {
                if (args[i] == "-u")
                {
                    // UDP Protocol

                    string ipSource = "";
                    int portSource = 0;
                    string ipTarget = "";
                    int portTarget = 0;
                    IPEndPoint source = null, target = null;

                    // Extract Source IP and Port
                    try
                    {
                        Match sourceMatch = _extractInfo.Match(args[i + 1]);

                        ipSource = sourceMatch.Groups[1].Value;
                        portSource = int.Parse(sourceMatch.Groups[2].Value);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("EXCEPTION: Extracting ip and port for source from args {0}: {1}", args[i + 1], e.Message);
                        PrintUsageAndExit();
                    }

                    // Extract Target IP and Port
                    try
                    {
                        Match targetMatch = _extractInfo.Match(args[i + 2]);

                        ipTarget= targetMatch.Groups[1].Value;
                        portTarget= int.Parse(targetMatch.Groups[2].Value);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("EXCEPTION: Extracting ip and port for target from args {0}: {1}", args[i + 2], e.Message);
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
                    UdpProxy proxy = new UdpProxy(
                            source, target);
                    // ---
                    proxies.Add(proxy);
                }
                else if (args[i] == "-t")
                {
                    // TCP Protocol
                    Console.WriteLine("ERROR: Tcp protocol not implemented yet");
                    PrintUsageAndExit();
                }
                else
                {
                    // :S ... Other protocols ? ...
                    Console.WriteLine("ERROR: Invalid protocol option {0}", args[i]);
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

        static void PrintUsageAndExit()
        {
            // FIXME: use better syntax ...
            Console.WriteLine("USAGE: Proxy.exe -u|t IPsource:port IPtarget:port ");
            Environment.Exit(0);
        }
    }
}
