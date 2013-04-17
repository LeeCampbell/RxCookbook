*In draft*

TIBCO Rendevous
===============

TIBCO's Rendevous product is a popular low latency point-to-point (unicast) and broadcast (multicast) communications platform.
Its interoperability (C, C++, Java, COM & .NET) have made it popular especially in the Captial Markets (Investment Banking) industry for distributing trade and price information internally.

The .NET Api is a fairly cumbersome port from I assume Java. 
Documentation is available from http://docs.tibco.com if you are happy to create a username & password to join the site *sigh*.
The aim of these snippets is to allow you to use TibRv with Rx and to follow the general principals of lazy evaluation and deterministic clean up.

TibRv basics
------------
TibRv (the common abbreviation for TIBCO Rendevous) has two main usages that will be covered here. 
First we will look at consuming Multicast messages. This is done by listening to a TibRv _Subject_ (not to be confused with an Rx `Subject`).
Second we will look at consuming Unicast message. TibRv does this with a specialisation of a TibRv _Subject_ called an _Inbox_.

###Subjects
A _Subject_ in TibRv is just a string that defines and Endpoint. Publishers will publish messages to a _Subject_ and consumers can listen to a specific _Subject_. 
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
 
In the first sample above, the consumer will receive any data that is published to the "Fx.Pricing.Spot.EURUSD" _Subject_. 
These could be [Foreign Exchange Rates](http://en.wikipedia.org/wiki/Exchange_rate) for the Euro [currency](http://en.wikipedia.org/wiki/Currency) to the US Dollar currency (i.e. the EUR/USD [Currency Pair](http://en.wikipedia.org/wiki/Currency_pair)).

In the second example, the consumer has subscribed to all _Subjects_ that start with "Fx.Pricing.Spot.". 
In practice this could get us Foreign Exchange Rates for other currencies, perhaps Euro to Great British Pound (Fx.Pricing.Spot.EURGBP), or New Zealand Dollar to Japanese Yen (Fx.Pricing.Spot.NZDJPY).

In the third example, the consumer will receive any data that is published to a _Subject_ that starts with "Fx.Pricing." and ends with "EURUSD".
In practice this could get us Foreign Exchange Rates for the EUR/USD Currency Pair, but for other [Financial Instruments](http://en.wikipedia.org/wiki/Foreign_exchange_market#Financial_instruments), not just "SPOT".
So we may receive "SPOT" data on the "Fx.Pricing.Spot.EURUSD" _Subject_, but we may also receive "Forward" rates on _Subjects_ like "Fx.Pricing.1W.EURUSD" for "1 Week Forward" data.

###Environment
In .NET you will need to specify 3 parameters that define the TibRv environment you want to connect to.
These 3 parameters are strings to define 
1. service
2. network
3. daemon

Generally these are fixed (in config) for a given deployment (i.e. different values for QA vs Live/Production).
