import {
  GraniteModule
} from "./chunk-A3JGM5OI.mjs";

// src/native-modules/natives/closeView.ts
async function closeView() {
  return GraniteModule.closeView();
}

// src/native-modules/natives/openURL.ts
import { Linking } from "react-native";
function openURL(url) {
  return Linking.openURL(url);
}
export {
  closeView,
  openURL
};
