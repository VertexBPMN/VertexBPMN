import { evaluate, parseExpression, parseUnaryTests, unaryTest } from './feelin.strict.mjs';

function parseContext(contextJson) {
  return contextJson ? revive(JSON.parse(contextJson)) : {};
}

function serialize(result) {
  return JSON.stringify(encode(result));
}

function temporalType(value) {
  if (value?.isLuxonDuration) return 'duration';
  if (!value?.isLuxonDateTime) return null;
  if (value.year === 1900 && value.month === 1 && value.day === 1) return 'time';
  if (value.hour === 0 && value.minute === 0 && value.second === 0
      && value.millisecond === 0 && value.offset === 0) return 'date';
  return 'date time';
}

function encode(value) {
  if (typeof value === 'bigint') return value.toString();
  const type = temporalType(value);
  if (type) {
    const includeOffset = value?.zone?.type !== 'system';
    const lexical = type === 'date'
      ? value.toISODate()
      : type === 'time'
        ? value.toISOTime({ suppressMilliseconds: true, includeOffset })
        : type === 'date time'
          ? value.toISO({ suppressMilliseconds: true, includeOffset })
          : value.toISO();
    return { $vertexFeelType: type, value: lexical };
  }
  if (Array.isArray(value)) return value.map(encode);
  if (value && Object.getPrototypeOf(value) === Object.prototype) {
    return Object.fromEntries(Object.entries(value).map(([key, entry]) => [key, encode(entry)]));
  }
  return value;
}

function revive(value) {
  if (Array.isArray(value)) return value.map(revive);
  if (!value || Object.getPrototypeOf(value) !== Object.prototype) return value;
  if (typeof value.$vertexFeelType === 'string' && typeof value.value === 'string') {
    const literal = JSON.stringify(value.value);
    const expression = value.$vertexFeelType === 'date time'
      ? `date and time(${literal})`
      : `${value.$vertexFeelType}(${literal})`;
    return evaluate(expression).value;
  }
  return Object.fromEntries(Object.entries(value).map(([key, entry]) => [key, revive(entry)]));
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
  context['?'] = inputJson === undefined ? null : revive(JSON.parse(inputJson));
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
