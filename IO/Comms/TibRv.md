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
First we will look at consuming Multicast messages. This is done by listening to a TibRv _Subject_ (not to be confused with an Rx `Subject`).
Second we will look at consuming Unicast message. TibRv does this with a sepcialisation of a TibRv _Subject_ called an _Inbox_.

So all a _Subject_ is really is a string that defines and Endpoint. Publishers will publish messages to a _Subject_ and consumers can listen to a specific _Subject_. 
Example _Subjects_:
 * dept.product.service.datatype
 * Fx.Pricing.Spot.EURUSD
 * users.login.notification
 * EComm.orders.accepted

A consumer will then need to know the _Subject_ that they want to subscribe to. A neat feature of TibRv is the ability to use wildcards when consuming events.
For example these are valid strings to use to subscribe to a subject
 * Fx.Pricing.Spot.EURUSD
 * Fx.Pricing.Spot.*
 * Fx.Pricing.*.EURUSD
