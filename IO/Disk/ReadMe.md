#Disk I/O with Rx
Reading and writing to the disk has for a long time on many platforms been faked to look like a synchronous activity.
As the disk is a separate system to the CPU, it can more naturally be thought of as a candidate for an asynchronous system.
Writes are really posts of data, and reads are really requests for data.

Modern platforms have embraced this concept with more popularity.
NodeJs for example performs all I/O asynchronously.
Recent versions of .NET have made a move towards asynchronous I/O to be the default way of interacting with network and disk subsystems.

In .NET this asynchronous approach is natively provided with the `Task` type and `async/await` keywords.
These are very effective, however only provide either a long pause while all data is sent or received, or only provide a chunk of data quickly. 
Rx can provided a much needed bridge for streaming data to and from the disk.


