Transport-Proxy
---------------

A simple transport layer packet proxy in C#. Currently only supports: 

* Udp (half-duplex: source -> target)
* Tcp (full-duplex: source <-> target, awaits source's request to start)

The tcp proxy implementation is a little unstable though ...
