_WIP: Place holder for comparison between TPL/AsyncAwait and Rx_

TODO:
 * Discuss What the TPL is
  * Task Parallel Library
  * Implementation of Futures/Promises
  * Introduces new concurrency primitives (Task, TaskPool)
  * Introduces Parallel Linq (PLinq) for parallelisation of processing `IEnumerable<T>` sequences
  * Plays nicely with async/await language features
  * Zero value (void/Unit) support with `Task`
  * Single value support with `Task<T>`
  * Eager semantics.
  * Allows for declarative coding of concurrent solutions
  * Continuation and error handling support

 * Discuss what Rx is
 * Discuss when to use TPL
 * Discuss when to use Rx
 * Discuss when neither are sensible
 * Discuss how to use them together
   * When this is just obvious
   * When it is less obvious, but could be useful
   * Point out the _perils_ of doing so
   * Point out the benefits of doing so
