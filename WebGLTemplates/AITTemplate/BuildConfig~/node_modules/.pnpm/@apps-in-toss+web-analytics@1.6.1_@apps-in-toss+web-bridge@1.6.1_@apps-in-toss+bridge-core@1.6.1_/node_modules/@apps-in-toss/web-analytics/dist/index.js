// src/index.ts
import { eventLog, env, getSchemeUri } from "@apps-in-toss/web-bridge";

// src/utils/extractDateFromUUIDv7.ts
var extractDateFromUUIDv7 = (uuid) => {
  const timestampHex = uuid.split("-").join("").slice(0, 12);
  const timestamp = Number.parseInt(timestampHex, 16);
  return new Date(timestamp);
};

// src/index.ts
var getReferrer = () => {
  try {
    const referrer = new URL(getSchemeUri());
    return referrer.searchParams.get("referrer");
  } catch {
    return "";
  }
};
var getGroupId = () => {
  try {
    const url = new URL(location.href);
    if (url.protocol !== "https:") {
      throw new Error("Invalid URL");
    }
    return {
      groupId: url.pathname,
      search: url.search.startsWith("?") ? url.search.substring(1) : url.search
    };
  } catch {
    return {
      groupId: "unknown",
      search: "unknown"
    };
  }
};
var Analytics = {
  screen: (params = {}) => {
    const { groupId, search } = getGroupId();
    if (groupId === "unknown") {
      return;
    }
    const { log_name, ...otherParams } = params;
    return eventLog({
      ...params,
      log_type: "screen",
      log_name: log_name ?? `${groupId}::screen`,
      params: {
        search,
        referrer: getReferrer(),
        deployment_id: env.getDeploymentId(),
        deployment_timestamp: extractDateFromUUIDv7(env.getDeploymentId()).getTime(),
        ...otherParams
      }
    });
  },
  impression: (params = {}) => {
    const { groupId, search } = getGroupId();
    if (groupId === "unknown") {
      return;
    }
    const { log_name, ...otherParams } = params;
    return eventLog({
      log_type: "event",
      log_name: log_name ?? `${groupId}::impression`,
      params: {
        ...otherParams,
        search,
        referrer: getReferrer(),
        deployment_id: env.getDeploymentId(),
        deployment_timestamp: extractDateFromUUIDv7(env.getDeploymentId()).getTime(),
        event_type: "impression"
      }
    });
  },
  click: (params = {}) => {
    const { groupId, search } = getGroupId();
    if (groupId === "unknown") {
      return;
    }
    const { log_name, ...otherParams } = params;
    return eventLog({
      log_type: "event",
      log_name: log_name ?? `${groupId}::click`,
      params: {
        ...otherParams,
        search,
        referrer: getReferrer(),
        deployment_id: env.getDeploymentId(),
        deployment_timestamp: extractDateFromUUIDv7(env.getDeploymentId()).getTime(),
        event_type: "click"
      }
    });
  }
};
export {
  Analytics
};
