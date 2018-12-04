FROM node:10-stretch as builder

RUN apt-get update && apt-get install apt-transport-https

# install dotnet
# (see https://www.microsoft.com/net/download/linux-package-manager/debian8/sdk-current)

RUN wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.asc.gpg
RUN mv microsoft.asc.gpg /etc/apt/trusted.gpg.d/
RUN wget -q https://packages.microsoft.com/config/debian/9/prod.list
RUN mv prod.list /etc/apt/sources.list.d/microsoft-prod.list
RUN chown root:root /etc/apt/trusted.gpg.d/microsoft.asc.gpg
RUN chown root:root /etc/apt/sources.list.d/microsoft-prod.list

RUN export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true && apt-get update && apt-get install -y dotnet-sdk-2.1

# install dependencies with npm
RUN npm install -global npx
RUN npm install -global pm2

# Bundle app source
WORKDIR /usr/src
COPY ove-asset-core ove-asset-core
COPY ove-service-imagetiles ove-service-imagetiles
WORKDIR /usr/src/ove-service-imagetiles

RUN npm install

# build
RUN npm run clean && npm run build

# The optimized docker image
FROM microsoft/dotnet:2.1-aspnetcore-runtime
WORKDIR /usr/src/ove-service-imagetiles

RUN apt-get update && apt-get install -y gnupg
RUN curl -sL https://deb.nodesource.com/setup_10.x | bash -
RUN apt-get update && apt-get install -y nodejs

RUN npm install -global pm2

# install libvips for tileservice
#https://github.com/phusion/baseimage-docker/issues/58
RUN echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections
RUN apt-get update && apt-get install -y libvips

COPY --from=builder /usr/src/ove-service-imagetiles/dist/ ./dist/
COPY --from=builder /usr/src/ove-service-imagetiles/pm2.json .
COPY --from=builder /usr/src/ove-service-imagetiles/package.json .

CMD [ "pm2-runtime", "pm2.json" ]

