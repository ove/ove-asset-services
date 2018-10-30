# Open Visualisation Environment

Open Visualisation Environment (OVE) is an open-source software stack, designed to be used in large high resolution display (LHRD) environments like the [Imperial College](http://www.imperial.ac.uk) [Data Science Institute's](http://www.imperial.ac.uk/data-science/) [Data Observatory](http://www.imperial.ac.uk/data-science/data-observatory/).

We welcome collaboration under our [Code of Conduct](https://github.com/ove/ove-services/blob/master/CODE_OF_CONDUCT.md).

## Build Instructions

The build system is based on [Lerna](https://lernajs.io/) using [Babel](http://babeljs.io/) for [Node.js](https://nodejs.org/en/) and uses a [PM2](http://pm2.keymetrics.io/) runtime.

### Prerequisites

* [git](https://git-scm.com/downloads)
* [Node.js](https://nodejs.org/en/) (v8.0+)
* [npm](https://www.npmjs.com/)
* [npx](https://www.npmjs.com/package/npx) `npm install -global npx`
* [PM2](http://pm2.keymetrics.io/) `npm install -global pm2`
* [Lerna](https://lernajs.io/)  `npm install -global lerna`

Building ``OVE.Service.ImageTiles`` also requires the [.NET Core command-line tools](https://docs.microsoft.com/en-us/dotnet/core/tools/?tabs=netcore2x) [Download here](https://www.microsoft.com/net/download/dotnet-core/2.0).

### Build

Setup the lerna environment:

* `git clone https://github.com/ove/ove-services`
* `cd ove-services`
* `lerna bootstrap --hoist`

Build and start runtime:

* `lerna run clean`
* `lerna run build`
* `pm2 start pm2.json`

### Stop

* `pm2 stop pm2.json`
* `pm2 delete pm2.json`

## Docker

Alternatively, you can use docker. Each package has its own docker image, which can be build by executing the build script or docker-compose directly.

### Development

If you are a developer who has made changes to your local copy of OVE services, and want to quickly rebuild it without rebuilding the docker container, you can run a container and mount the code as a volume:

```sh
cd /some/path/to/ove-services
docker run -it -p 8181-8190:8181-8190 -v $PWD:/code ove-services bash
```

and then, inside the container, run:

```sh
cd /code
lerna bootstrap --hoist && lerna run clean && lerna run build
pm2 start pm2.json
```
