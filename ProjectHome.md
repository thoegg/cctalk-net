API based on ccTalk generic specification [issue 4](https://code.google.com/p/cctalk-net/issues/detail?id=4).6

It is basic C# .Net assembly, built for 3.5 framework.

Current release supports main commands with simple checksum. It`s ready for integration with coin acceptor (tested with Microcoin SP). Other functionality are mostly "TODO".

UPD: added support for bill validators (checked with "ardac Elite Note Validator", Thanks velteyn for contribution)

### How to start ###
  * Create CoinAcceptor class (set port number on constructor)
  * Subscribe to events.
  * Call Init (opens port)
  * Call StartPoll (starts polling for events)
  * Events raised when coin accepted or rejected
OR study cctalk-apptest project. It contains demonstration of usage.

### History ###
This project is based on [other ccTalk project](http://code.google.com/p/libcctalk/). Sine then it was completely rewritten to current state in "ДатаКрат-Е" company and moved to open source.

### About ccTalk protocol ###
http://en.wikipedia.org/wiki/CcTalk