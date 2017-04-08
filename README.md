# SockLib

A client/server socket library that manages messages in a variety of formats, including text, binary and multiple file uploads and downloads.

In some ways, SockLib is similar to a web server used as an application server, but has several advantages. Its main obvious disadvantage is that it uses its own protocol, can be used only through its own API, and cannot interact with a web browser.

The advantages are:

1. It is lightweight and implemented in a small dll, included in both client and server.
2. The client and server can work in Transaction mode, where the client sends a message and waits for a reply, like a webserver. However, they can also work in Listening mode, where the connection is kept open and either end can send messages at any time.
3. It does not use the .NET http server facilities (as used by the [Babbacombe.Webserver](https://github.com/trevorprinn/Babbacombe.Webserver)), which are not included as part of Xamarin. This means that the Xamarin SockLib build can be used to turn an Android or iOS device into a server (handy, for example, for writing a PC based companion app for an tablet app).
4. It includes a UDP based discovery server. This allows a client to find a SockLib server running on the local network without needing it to have a known IP address. 
5. The library includes a strong public key cryptography facility (Windows only at present), that does not require the clients and server to have a predefined shared key. 

.NET, Android and iOS builds of the library are available from the [Babbacombe myget (nuget) feed](https://www.myget.org/gallery/babbacom-feed)    
https://www.myget.org/F/babbacom-feed/api/v2

See the [project wiki](https://github.com/trevorprinn/Babbacombe.SockLib/wiki) for more information. 

This library is licenced under the LGPL 2.1 licence.
