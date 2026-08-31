import {
  evaluate as evaluateRaw,
  parseExpression,
  parseUnaryTests,
  unaryTest as unaryTestRaw
} from './feelin.strict.mjs';

function normalizeAtLiterals(expression) {
  let normalized = '';
  for (let index = 0; index < expression.length;) {
    const atLiteral = expression[index] === '@' && expression[index + 1] === '"';
    if (expression[index] !== '"' && !atLiteral) {
      const character = expression[index];
      const previous = index > 0 ? expression[index - 1] : '';
      const next = index + 1 < expression.length ? expression[index + 1] : '';
      normalized += character === "'" && /[\p{L}\p{N}]/u.test(previous) && /[\p{L}\p{N}]/u.test(next)
        ? '_'
        : character;
      index++;
      continue;
    }

    const quoteStart = atLiteral ? index + 1 : index;
    let end = quoteStart + 1;
    while (end < expression.length) {
      if (expression[end] === '\\') {
        end += 2;
        continue;
      }
      if (expression[end++] === '"') break;
    }
    const literal = expression.slice(quoteStart, end);
    if (!atLiteral) {
      normalized += literal;
      index = end;
      continue;
    }

    const value = JSON.parse(literal);
    let constructor;
    if (/^-?P/.test(value)) constructor = 'duration';
    else if (/^-?(?:\d{4}|[1-9]\d{4,8})-\d{2}-\d{2}T/.test(value)) constructor = 'date and time';
    else if (value.includes(':')) constructor = 'time';
    else if (/^-?[0-9]+-[0-9]{2}-[0-9]{2}/.test(value)) constructor = 'date';
    normalized += constructor
      ? `${constructor}(${literal})`
      : `__vertexInvalidAtLiteral(${literal})`;
    index = end;
  }
  return normalized.replace(/\brange\s*<\s*([^<>]+?)\s*>/gi, (_match, type) =>
    `range__${type.trim().toLowerCase().replace(/\s+/g, '_')}`);
}

function evaluate(expression, context = {}) {
  return evaluateRaw(normalizeAtLiterals(expression), context);
}

function unaryTest(expression, context = {}) {
  return unaryTestRaw(normalizeAtLiterals(expression), context);
}

function parseContext(contextJson) {
  if (!contextJson) return {};
  const parsed = JSON.parse(contextJson);
  return Object.fromEntries(Object.entries(parsed).map(([key, value]) => [
    key.replace(/([\p{L}\p{N}])'(?=[\p{L}\p{N}])/gu, '$1_'),
    revive(value)
  ]));
}

function serialize(result) {
  return JSON.stringify(encode(result));
}

function temporalType(value) {
  if (typeof value?.$feelTemporalType === 'string') return value.$feelTemporalType;
  if (value?.isLuxonDuration) return 'duration';
  if (!value?.isLuxonDateTime) return null;
  if (value.year === 1900 && value.month === 1 && value.day === 1) return 'time';
  if (value.hour === 0 && value.minute === 0 && value.second === 0
      && value.millisecond === 0 && value.offset === 0) return 'date';
  return 'date time';
}

function durationLexical(value) {
  const negative = value.valueOf() < 0;
  const absolute = negative ? value.negate() : value;
  const units = absolute.toObject();
  const sign = negative ? '-' : '';
  if (value.$feelDurationKind === 'year-month'
      || Object.prototype.hasOwnProperty.call(units, 'years')
      || Object.prototype.hasOwnProperty.call(units, 'months')) {
    const shifted = absolute.shiftTo('years', 'months');
    return sign + (shifted.as('months') === 0 ? 'P0M' : shifted.toISO());
  }
  return sign + absolute.shiftTo('days', 'hours', 'minutes', 'seconds').toISO();
}

function canonicalTemporalLexical(value) {
  return value
    .replace(/^-(\d+)(?=-)/, (_match, year) => `-${year.replace(/^0+(?=\d{4})/, '')}`)
    .replace(/^\+(\d+)(?=-)/, (_match, year) => year.replace(/^0+(?=\d{4})/, ''));
}

function encode(value) {
  if (typeof value === 'bigint') return value.toString();
  const type = temporalType(value);
  if (type) {
    const includeOffset = value?.zone?.type !== 'system';
    const lexical = value.$feelLexical ?? (type === 'date'
      ? value.toISODate()
      : type === 'time'
        ? value.toISOTime({ suppressMilliseconds: true, includeOffset })
        : type === 'date time'
          ? value.toISO({ suppressMilliseconds: true, includeOffset })
          : durationLexical(value));
    return { $vertexFeelType: type, value: canonicalTemporalLexical(lexical) };
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
  const input = inputJson === undefined ? null : revive(JSON.parse(inputJson));
  context['?'] = input;
  if (typeof input === 'boolean' && !expression.includes('?')) {
    const candidate = evaluate(expression, context);
    if (candidate.warnings.length === 0 && typeof candidate.value === 'boolean') {
      return serialize({ value: candidate.value === input, warnings: [] });
    }
  }
  return serialize(unaryTest(expression, context));
};

globalThis.vertexFeelValidateExpression = function vertexFeelValidateExpression(expression) {
  assertCompleteSyntax(parseExpression(normalizeAtLiterals(expression)), 'FEEL expression');
  return 'true';
};

globalThis.vertexFeelValidateUnaryTests = function vertexFeelValidateUnaryTests(expression) {
  assertCompleteSyntax(parseUnaryTests(normalizeAtLiterals(expression)), 'FEEL unary test');
  return 'true';
};
