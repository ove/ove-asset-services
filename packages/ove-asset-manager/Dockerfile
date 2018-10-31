FROM node:10-stretch as builder
WORKDIR /usr/src/app

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
COPY . .

RUN npm install

# build
RUN npm run clean && npm run build:alpine

# The optimized docker image
FROM microsoft/dotnet:2.1-runtime-alpine
WORKDIR /usr/src/app

RUN apk add --no-cache nodejs nodejs-npm

RUN npm install -global pm2

COPY --from=builder /usr/src/app/dist/ ./dist/
COPY --from=builder /usr/src/app/pm2.json .
COPY --from=builder /usr/src/app/package.json .

EXPOSE 80

CMD [ "pm2-runtime", "pm2.json" ]

