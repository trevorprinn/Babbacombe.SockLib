# SockLib

A client/server socket library that manages messages in a variety of formats, including text, binary and multiple file uploads and downloads.

This library is currently Work In Progress, and the API is subject to change. The wiki and the internal documentation are not yet complete.

In some ways, SockLib is similar to a web server used as an application server, but has several advantages. Its main obvious disadvantage is that it uses its own protocol, can be used only through its own API, and cannot interact with a web browser.

The advantages are:

1. It is lightweight and implemented in a small dll, included in both client and server.
2. The client and server can work in Transaction mode, where the client sends a message and waits for a reply, like a webserver. However, they can also work in Listening mode, where the connection is kept open and either end can send messages at any time.
3. It does not use the .NET http server facilities (as used by the [Babbacombe.Webserver](https://github.com/trevorprinn/Babbacombe.Webserver)), which are not included as part of Xamarin. This means that the Xamarin SockLib build can be used to turn an Android device into a server (handy, for example, for writing a PC based companion app for an Android app). I don't have Xamarin iOS as yet, but it should be very easy to make a build for that as well from the source.
4. It includes a UDP based discovery server. This allows a client to find a SockLib server running on the local network without needing it to have a known IP address.  

.NET and Android builds of the library are available from the [Babbacombe myget (nuget) feed](https://www.myget.org/gallery/babbacom-feed)    
https://www.myget.org/F/babbacom-feed/api/v2

This library is licenced under the LGPL 2.1 licence.
