import { evaluate, parseExpression, parseUnaryTests, unaryTest } from 'feelin';

function parseContext(contextJson) {
  return contextJson ? JSON.parse(contextJson) : {};
}

function serialize(result) {
  return JSON.stringify(result, (_key, value) => {
    if (typeof value === 'bigint') return value.toString();
    return value;
  });
}

function assertCompleteSyntax(tree, kind) {
  const cursor = tree.cursor();
  do {
    if (cursor.type.isError) {
      throw new Error(`Invalid ${kind} syntax at offset ${cursor.from}`);
    }
  } while (cursor.next());
}

globalThis.vertexFeelEvaluate = function vertexFeelEvaluate(expression, contextJson) {
  return serialize(evaluate(expression, parseContext(contextJson)));
};

globalThis.vertexFeelUnaryTest = function vertexFeelUnaryTest(expression, inputJson, contextJson) {
  const context = parseContext(contextJson);
  context['?'] = inputJson === undefined ? null : JSON.parse(inputJson);
  return serialize(unaryTest(expression, context));
};

globalThis.vertexFeelValidateExpression = function vertexFeelValidateExpression(expression) {
  assertCompleteSyntax(parseExpression(expression), 'FEEL expression');
  return 'true';
};

globalThis.vertexFeelValidateUnaryTests = function vertexFeelValidateUnaryTests(expression) {
  assertCompleteSyntax(parseUnaryTests(expression), 'FEEL unary test');
  return 'true';
};
