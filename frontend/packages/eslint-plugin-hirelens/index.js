const HEX = /#(?:[0-9a-fA-F]{3,8})\b/;
const COLOR_FN = /\b(?:rgb|rgba|hsl|hsla|oklch|oklab)\s*\(/;
const ARBITRARY_SIZE = /\[[0-9.]+(?:px|rem|em)\]/;

function reportIfHardcoded(context, node, value) {
  if (typeof value !== "string") {
    return;
  }

  if (HEX.test(value) || COLOR_FN.test(value) || ARBITRARY_SIZE.test(value)) {
    context.report({
      node,
      message: "Use design tokens. Hard-coded colors, type sizes, and spacing are forbidden."
    });
  }
}

const noHardcodedDesignValues = {
  meta: {
    type: "problem",
    docs: {
      description: "Forbid hard-coded color, type, and spacing values outside design tokens."
    }
  },
  create(context) {
    return {
      Literal(node) {
        if (typeof node.value === "string") {
          reportIfHardcoded(context, node, node.value);
        }
      },
      TemplateElement(node) {
        reportIfHardcoded(context, node, node.value.raw);
      }
    };
  }
};

export default {
  rules: {
    "no-hardcoded-design-values": noHardcodedDesignValues
  }
};
