# Smart batching

An alternative to polling at a fixed time period is to have an adaptive or variable time period to poll.
I have used similar techniques to emulate a queue where a _Reliable Messaging_ system was not available.

In this scenario, the requirements are to poll for data and when there is data, immediately re-poll once processing the data is complete.
If there is no data, then delay polling by some period.
If on subsequent polling there is still no data, poll again, but with an extended period.

* On successfully receiving data, poll again immediately
* If no data available, then further delay polling up to a maximum period.
* Log errors, but do not allow this to stop polling. Consider failures to be "no data".
