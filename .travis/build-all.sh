#!/bin/bash

scriptPath=$(dirname "$(readlink -f "$0")")

${scriptPath}/../packages/ove-asset-manager/build.sh && \
${scriptPath}/../packages/ove-service-imagetiles/build.sh && \
${scriptPath}/../packages/ove-service-archives/build.sh