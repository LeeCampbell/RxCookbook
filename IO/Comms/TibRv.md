TIBCO Rendevous
===============

TIBCO's Rendevous product is a popular low latency point-to-point (unicast) and broadcast (multicast) communications platform.
Its interoperability (C, C++, Java, COM & .NET) have made it popular especially in the Captial Markets (Investment Banking) industry for distributing trade and price information internally.

The .NET Api is a fairly cumbersome port from I assume Java. To get decent support you need to login to the TIBCO website with account information that is not often available to the "Developer on the floor".
This only makes things more difficult to get right.
The aim of these snippets is to allow you to use TibRv with Rx and to follow the general principals of lazy evaluation and deterministic clean up.

TibRv basics
------------
TibRv (the common abbreviation for TIBCO Rendevous) has two main usages that will be covered here. 
First we will look at consuming Multicast messages. This is done by listening to a TibRv Subject (not to be confused with an Rx Subject).
Second we will look at consuming Unicast message. TibRv does this with a sepcialisation of a TibRv Subject called an Inbox.

