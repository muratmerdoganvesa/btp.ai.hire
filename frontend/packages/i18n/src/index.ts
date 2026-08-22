import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./en.json";
import tr from "./tr.json";

export function createI18n() {
  void i18n.use(initReactI18next).init({
    lng: "tr",
    fallbackLng: "en",
    interpolation: { escapeValue: false },
    resources: {
      tr: { translation: tr },
      en: { translation: en }
    }
  });

  return i18n;
}

export { i18n };
