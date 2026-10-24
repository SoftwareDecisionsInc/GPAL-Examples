# GPAL Examples

Ready-to-run programs built on [GPAL](https://gpal.sditx.com/), the Generally Positive Automation Library. Each one
shows one part of GPAL from start to finish, with the whole workflow in its source.

| Program | What it shows |
|---|---|
| [SimpleScraper](./SimpleScraper) | The starter scrape: searches eBay, reads three pages of results and saves description, price and link to a tab-delimited file. |
| [InptFileTest](./InptFileTest) | The same eBay search driven by an input file, one search per line of ebay.words.txt, two result pages per term. |
| [InputGridChaining](./InputGridChaining) | Three eBay workflows chained through an input grid, with CallAfterFillIn, CallIfFound and a persistent popup handler. |
| [GherkinTest](./GherkinTest) | The SimpleScraper search written as a Given/When/Then scenario with GPAL.Gherkin. |
| [BookList](./BookList) | Reads Goodreads Best Books Ever, then searches Barnes and Noble for each highly rated book and collects title, author and price. |
| [FetchTest](./FetchTest) | Calls a store's search API with Fetch from inside the page, over three search terms, and saves the JSON. |
| [BrowserRESTClientTest](./BrowserRESTClientTest) | Signs in with a browser, then hands the session to a plain REST client with ContinueAsRESTClient. |
| [GPALFileUrlAndArchiveTest](./GPALFileUrlAndArchiveTest) | GPALFile from a url: conditional download, numbered archive copies, and a download that needs the browser's session. |
| [WorkflowPagingTest](./WorkflowPagingTest) | Pages through Hacker News with Until and While loops on the OttoMagic and Puppeteer clients. |
| [OpenAPITester](./OpenAPITester) | Loads an OpenAPI map, lists its endpoints, calls them and pretty-prints the answers. No setup. |
| [ConverterTestor](./ConverterTestor) | The GPAL converter, blind, in every direction: a class with every data type to YAML, CSV, JSON, HTML and XML and back, collections, grids, an Excel workbook, and negative tests whose warnings are expected. No setup. |
| [GPALFormsTest](./GPALFormsTest) | A tour of the GPALForm controls, plus Start, Continue and Hand Off: a second program picking up the browser the first one left running. |

For complete programs that do a real job, see [Bot of the Week](https://github.com/SoftwareDecisionsInc/BotOfTheWeek):
a new one every week, each with a video and a walkthrough of its code.

## Build

Open `GPAL-Examples.sln` in Visual Studio 2022 (.NET Framework 4.8) and build. Every program builds against the
GPAL package on nuget.org, version 1.0.2, which Visual Studio restores the first time. Each program also
opens on its own through its project file.

## Running them

- **Drivers.** Selenium's drivers are kept beside each exe and updated to match your browsers automatically.
- **Profiles.** The browser programs default to `C:\profiles\chrome\signedin`, `C:\profiles\edge\signedin` and
  `C:\profiles\firefox\signedin`. Point the Profiles tab at your own profile folders, or clear a box and GPAL runs
  that browser on a temporary profile it removes afterwards.
- **OttoMagic.** The OttoMagic engine starts unticked. It needs the OttoMagic extension installed in the profile it
  runs on, and GPALRestAPI installed on the machine; both are at https://gpal.sditx.com/Ottomagic, which each
  program's Run tab also links to. The other engines need nothing installed.

Docs, tutorials and videos: https://gpal.sditx.com

## This repository

This is the read-only public release mirror of the GPAL example programs. Development happens privately, and pull
requests are not accepted here.

## License

Source available under the **Business Source License 1.1**, with the same Additional Use Grant as GPAL. Full terms:
[LICENSE.md](./LICENSE.md). Privacy: [privacy.md](./privacy.md).

Copyright (c) 2026 Software Decisions, Inc. All rights reserved.