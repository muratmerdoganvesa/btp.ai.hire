import hirelens from "eslint-plugin-hirelens";
import tseslint from "typescript-eslint";

export default [
  {
    ignores: ["**/dist/**", "**/node_modules/**"]
  },
  ...tseslint.configs.recommended,
  {
    files: ["apps/**/*.{ts,tsx}", "packages/ui/**/*.{ts,tsx}"],
    plugins: { hirelens },
    rules: {
      "hirelens/no-hardcoded-design-values": "error",
      "@typescript-eslint/no-explicit-any": "error"
    }
  }
];
