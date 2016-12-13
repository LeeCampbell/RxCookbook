# Polling with Rx

Often if a system is not already inherently Reactive, we may resort to polling to give the illusion of a reactive interface.
This can happen at many layers.
You may need to poll a datastore (database/filesystem) if it can not emit change notifications.
You may need to poll an HTTP endpoint if a WebSockets endpoint is not exposed.

While embracing a Reactive first platform is preferred as it should be less resource intensive, polling can be a necessary intermediate step to full adoption of a Reactive System (https://youtu.be/np2aIaojc10).

# Constant cadence polling
While it may seem somewhat obvious to experienced Rx practitioners how you may go about polling with Rx, there are still a few curve balls depending on your exact requirements.
So lets specify what our goals will be.

I think that most use cases of polling want the following semantics:
 * Constant cadence/frequency of polling
 * Only start re-polling once the previous request has completed. This is important if you don't want to have over lapping requests.
 * Log errors, but do not allow this to stop polling.



## Creating our tests

# Adaptive Polling - Exponential backoff

An alternative to polling at a fixed time period is to have an adaptive or variable time period to poll.
I have used this technique to emulate a queue where a _Reliable Messaging_ system was not available.

In this scenario, the requirements are to poll for data and when there is data, immediately re-poll once processing the data is complete.
If there is no data, then delay polling by some period.
If on subsequent polling there is still no data, poll again, but with an extended period.

* On successfully receiving data, poll again immediately
* If no data available, then further delay polling up to a maximum period.
* Log errors, but do not allow this to stop polling. Consider failures to be "no data".

See also:
 * http://www.enterpriseintegrationpatterns.com/patterns/conversation/Polling.html
 * http://www.enterpriseintegrationpatterns.com/patterns/messaging/PollingConsumer.html
