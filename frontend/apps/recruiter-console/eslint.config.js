import hirelens from "eslint-plugin-hirelens";
import tseslint from "typescript-eslint";

export default [
  ...tseslint.configs.recommended,
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: { hirelens },
    rules: {
      "hirelens/no-hardcoded-design-values": "error",
      "@typescript-eslint/no-explicit-any": "error"
    }
  }
];
