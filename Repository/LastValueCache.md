
_IN DRAFT_

#Requirements

 * State of the World (ie. new and late subscribers can get the state of the world)
 * Updates to individual values (only if it actually changes (IComparable/IEquatable))
 * Can get a SoW snapshot by using Take(1)
 * Offers the ability to provide a "Subscribe to live (and cache), fetch Snapshot, push snapshot, then run in the cached live values, then continue with live"
 
People looking for this sln

http://social.msdn.microsoft.com/Forums/en-US/rx/thread/28e1bf19-f816-4b24-b964-7807cf1aaaf9
(duplicated on SO http://stackoverflow.com/questions/17107924/rx-how-to-concat-a-snapshot-stream-and-an-update-stream)


