import { readFileSync, writeFileSync } from 'node:fs';

const sourcePath = new URL('./node_modules/feelin/dist/index.js', import.meta.url);
const targetPath = new URL('./feelin.strict.mjs', import.meta.url);
let source = readFileSync(sourcePath, 'utf8');

const replacements = [
  [
    `function getType(e) {
    if (isNil(e)) {`,
    `function coerceFeelRuntimeValue(value, type) {
    if (!type || value === null || typeof value === 'undefined') return value;
    if (matchesFeelType(value, type)) return value;
    const listType = /^list\\s*<([\\s\\S]+)>$/i.exec(type);
    if (listType && matchesFeelType(value, listType[1])) return [value];
    if (!listType && isArray(value) && value.length === 1 && matchesFeelType(value[0], type)) return value[0];
    throw new Error(\`Value does not match FEEL type \${type}\`);
}
function getType(e) {
    if (e && typeof e.$feelTemporalType === 'string') return e.$feelTemporalType;
    if (isNil(e)) {`
  ],
  [
    `        case 'FormalParameter': return args[0];`,
    `        case 'FormalParameter': return { name: args[0], type: args[1] ? args[1]({}) : null };`
  ],
  [
    `            const parameterNames = args[2];
            const fnBody = args[4];`,
    `            const parameterDefinitions = args[2].map(parameter =>
                typeof parameter === 'string' ? { name: parameter, type: null } : parameter);
            const parameterNames = parameterDefinitions.map(parameter => parameter.name);
            const fnBody = args[4];`
  ],
  [
    `                    context[name] = args[idx];`,
    `                    context[name] = coerceFeelRuntimeValue(args[idx], parameterDefinitions[idx].type);`
  ],
  [
    `function toString(obj, wrap = false) {`,
    `function canonicalTemporalLexical(value) {
    return value
        .replace(/^-(\\d+)(?=-)/, (_match, year) => '-' + year.replace(/^0+(?=\\d{4})/, ''))
        .replace(/^\\+(\\d+)(?=-)/, (_match, year) => year.replace(/^0+(?=\\d{4})/, ''));
}
function toString(obj, wrap = false) {`
  ],
  [
    `    if (isString(e)) {
        return 'string';
    }
    if (isContext(e)) {`,
    `    if (isString(e)) {
        return 'string';
    }
    if (typeof e === 'function') {
        return 'function';
    }
    if (isContext(e)) {`
  ],
  [
    `        return DateTime.fromISO(str.toUpperCase(), {
            setZone: true,
            zone
        });`,
    `        let parseValue = str.toUpperCase();
        const yearMatch = /^(-?)(\\d{4,9})-/.exec(parseValue);
        const signedYear = yearMatch ? Number((yearMatch[1] || '') + yearMatch[2]) : null;
        if (signedYear !== null && Math.abs(signedYear) <= 275760) {
            if (signedYear < 0 && Math.abs(signedYear) < 10000) {
                const positiveValue = parseValue.slice(1);
                return DateTime.fromISO(positiveValue, { setZone: true, zone }).set({ year: signedYear });
            }
            if (Math.abs(signedYear) >= 10000) {
                const sign = signedYear < 0 ? '-' : '+';
                parseValue = sign + String(Math.abs(signedYear)).padStart(6, '0') + parseValue.slice(yearMatch[0].length - 1);
            }
        }
        return DateTime.fromISO(parseValue, {
            setZone: true,
            zone
        });`
  ],
  [
    `    const type = getType(obj);
    if (type === 'nil') {`,
    `    const type = getType(obj);
    if (obj && obj.$feelLexical && (type === 'date' || type === 'time' || type === 'date time')) {
        return canonicalTemporalLexical(obj.$feelLexical);
    }
    if (type === 'nil') {`
  ],
  [
    `            return obj.toISO({ suppressMilliseconds: true, includeOffset: false });`,
    `            return canonicalTemporalLexical(obj.toISO({ suppressMilliseconds: true, includeOffset: false }));`
  ],
  [
    `            return obj.toISO({ suppressMilliseconds: true, includeOffset: false }) + '@' + ((_b = obj.zone) === null || _b === void 0 ? void 0 : _b.zoneName);`,
    `            return canonicalTemporalLexical(obj.toISO({ suppressMilliseconds: true, includeOffset: false })) + '@' + ((_b = obj.zone) === null || _b === void 0 ? void 0 : _b.zoneName);`
  ],
  [
    `        return obj.toISO({ suppressMilliseconds: true });`,
    `        return canonicalTemporalLexical(obj.toISO({ suppressMilliseconds: true }));`
  ],
  [
    `        return obj.toISODate();`,
    `        return canonicalTemporalLexical(obj.toISODate());`
  ],
  [
    `            return obj.toISOTime({ suppressMilliseconds: true, includeOffset: false });`,
    `            return canonicalTemporalLexical(obj.toISOTime({ suppressMilliseconds: true, includeOffset: false }));`
  ],
  [
    `            return obj.toISOTime({ suppressMilliseconds: true, includeOffset: false }) + '@' + ((_d = obj.zone) === null || _d === void 0 ? void 0 : _d.zoneName);`,
    `            return canonicalTemporalLexical(obj.toISOTime({ suppressMilliseconds: true, includeOffset: false })) + '@' + ((_d = obj.zone) === null || _d === void 0 ? void 0 : _d.zoneName);`
  ],
  [
    `        return obj.toISOTime({ suppressMilliseconds: true });`,
    `        return canonicalTemporalLexical(obj.toISOTime({ suppressMilliseconds: true }));`
  ],
  [
    `function date(str = null, time = null, zone = null) {`,
    `function isLeapYear(year) {
    return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
}
function assertValidDateParts(year, month, day) {
    if (!Number.isInteger(year) || year === 0 || year < -999999999 || year > 999999999
        || !Number.isInteger(month) || month < 1 || month > 12
        || !Number.isInteger(day) || day < 1) {
        throw new Error('invalid FEEL date');
    }
    const days = [31, isLeapYear(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    if (day > days[month - 1]) throw new Error('invalid FEEL date');
}
function validateDateLexical(value) {
    const match = /^(-?)(\\d{4}|[1-9]\\d{4,8})-(\\d{2})-(\\d{2})$/.exec(value);
    if (!match) throw new Error('invalid FEEL date lexical form');
    const year = Number((match[1] || '') + match[2]);
    assertValidDateParts(year, Number(match[3]), Number(match[4]));
}
function validateTimeLexical(value) {
    const match = /^(\\d{2}):(\\d{2}):(\\d{2})(?:\\.(\\d{1,9}))?(?:(Z)|([+-])(\\d{2}):(\\d{2})(?::(\\d{2}))?|@([^@]+))?$/.exec(value);
    if (!match) throw new Error('invalid FEEL time lexical form');
    const hour = Number(match[1]);
    const minute = Number(match[2]);
    const second = Number(match[3]);
    if (hour > 24 || minute > 59 || second > 59 || (hour === 24 && (minute !== 0 || second !== 0 || /[1-9]/.test(match[4] || ''))))
        throw new Error('invalid FEEL time');
    if (match[6]) {
        const offsetHour = Number(match[7]);
        const offsetMinute = Number(match[8]);
        const offsetSecond = Number(match[9] || 0);
        if (offsetHour > 14 || offsetMinute > 59 || offsetSecond > 59
            || (offsetHour === 14 && (offsetMinute !== 0 || offsetSecond !== 0)))
            throw new Error('invalid FEEL time offset');
    }
    if (match[10] && !Info.normalizeZone(match[10]).isValid)
        throw new Error('invalid FEEL timezone');
}
function validateTemporalLexical(value) {
    const separator = value.indexOf('T');
    if (separator < 0) {
        if (value.includes(':')) validateTimeLexical(value);
        else validateDateLexical(value);
        return;
    }
    if (separator === 0 || separator === value.length - 1)
        throw new Error('invalid FEEL date and time lexical form');
    validateDateLexical(value.slice(0, separator));
    validateTimeLexical(value.slice(separator + 1));
}
function extendedTemporal(type, lexical) {
    return { $feelTemporalType: type, $feelLexical: lexical };
}
function feelDateLexical(year, month, day) {
    const sign = year < 0 ? '-' : '';
    const yearText = String(Math.abs(year)).padStart(4, '0');
    return \`\${sign}\${yearText}-\${String(month).padStart(2, '0')}-\${String(day).padStart(2, '0')}\`;
}
function date(str = null, time = null, zone = null) {`
  ],
  [
    `    if (typeof str === 'string') {
        if (str.startsWith('-')) {
            throw notImplemented('negative date');
        }`,
    `    if (typeof str === 'string') {
        validateTemporalLexical(str);
        const lexicalYear = /^(-?\\d{4,9})-/.exec(str);
        if (lexicalYear && Math.abs(Number(lexicalYear[1])) > 275760)
            return extendedTemporal(str.includes('T') ? 'date time' : 'date', str);`
  ],
  [
    `    'date': fn(function (year, month, day, from) {
        if (!from && !isNumber(year)) {`,
    `    'date': fn(function (year, month, day, from) {
        if (!from && !isNumber(year)) {`
  ],
  [
    `        if (year) {
            if (!isNumber(month) || !isNumber(day)) {
                return null;
            }
            d = date().setZone('utc').set({`,
    `        if (isNumber(year)) {
            assertValidDateParts(year, month, day);
            if (Math.abs(year) > 275760) return extendedTemporal('date', feelDateLexical(year, month, day));
            d = date().setZone('utc').set({`
  ],
  [
    `        return d && ifValid(d.setZone('utc').startOf('day')) || null;
    }, ['any?', 'number?', 'number?', 'any?'], ['year', 'month', 'day', 'from']),`,
    `        if (d?.$feelTemporalType === 'date') return d;
        if (!d || !d.isValid) throw new Error('invalid FEEL date');
        const result = d.setZone('utc', { keepLocalTime: true }).startOf('day');
        if (isString(from)) result.$feelLexical = from;
        else if (isNumber(year)) {
            result.$feelLexical = feelDateLexical(year, month, day);
        }
        return result;
    }, ['any?', 'number?', 'number?', 'any?'], ['year', 'month', 'day', 'from']),`
  ],
  [
    `        return dt && ifValid(dt) || null;
    }, ['any?', 'time?', 'string?'], ['date', 'time', 'from']),`,
    `        if (dt?.$feelTemporalType === 'date time') return dt;
        if (!dt || !dt.isValid) throw new Error('invalid FEEL date and time');
        if (isString(from)) dt.$feelLexical = from;
        else if (isDateTime(d) && isDateTime(time)) {
            const dateLexical = (d.$feelLexical || d.toISODate()).split('T', 1)[0];
            const timeLexical = time.$feelLexical || time.toISOTime({ suppressMilliseconds: true, includeOffset: time.zone.type !== 'system' });
            dt.$feelLexical = \`\${dateLexical}T\${timeLexical}\`;
        }
        dt.$feelTemporalType = 'date time';
        return dt;
    }, ['any?', 'time?', 'string?'], ['date', 'time', 'from']),`
  ],
  [
    `        if (isNumber(hour)) {
            if (!isNumber(minute) || !isNumber(second)) {
                return null;
            }
            // TODO: support offset = days and time duration`,
    `        if (isNumber(hour)) {
            if (!Number.isInteger(hour) || hour < 0 || hour > 23
                || !Number.isInteger(minute) || minute < 0 || minute > 59
                || typeof second !== 'number' || second < 0 || second >= 60) {
                throw new Error('invalid FEEL time');
            }
            // TODO: support offset = days and time duration`
  ],
  [
    `        return t && ifValid(t) || null;
    }, ['any?', 'number?', 'number?', 'any?', 'any?'], ['hour', 'minute', 'second', 'offset', 'from']),`,
    `        if (!t || !t.isValid) throw new Error('invalid FEEL time');
        if (isString(from)) t.$feelLexical = from;
        else if (isDateTime(from) && from.$feelLexical && from.$feelLexical.includes('T'))
            t.$feelLexical = from.$feelLexical.split('T').slice(1).join('T');
        else if (isNumber(hour) && (second % 1 !== 0 || (offset && offset.as('seconds') % 60 !== 0))) {
            const wholeSecond = Math.floor(second);
            const fraction = second % 1 === 0 ? '' : String(second).split('.')[1].replace(/0+$/, '');
            let offsetText = '';
            if (offset) {
                const totalSeconds = Math.trunc(offset.as('seconds'));
                const absolute = Math.abs(totalSeconds);
                const offsetHours = Math.floor(absolute / 3600);
                const offsetMinutes = Math.floor(absolute % 3600 / 60);
                const offsetSeconds = absolute % 60;
                offsetText = \`\${totalSeconds < 0 ? '-' : '+'}\${String(offsetHours).padStart(2, '0')}:\${String(offsetMinutes).padStart(2, '0')}\${offsetSeconds ? ':' + String(offsetSeconds).padStart(2, '0') : ''}\`;
            }
            t.$feelLexical = \`\${String(hour).padStart(2, '0')}:\${String(minute).padStart(2, '0')}:\${String(wholeSecond).padStart(2, '0')}\${fraction ? '.' + fraction : ''}\${offsetText}\`;
        }
        return t;
    }, ['any?', 'number?', 'number?', 'any?', 'any?'], ['hour', 'minute', 'second', 'offset', 'from']),`
  ],
  [
    `    constructor(fn, parameterNames) {
        this.fn = fn;
        this.parameterNames = parameterNames;
    }`,
    `    constructor(fn, parameterNames, strictArity = false) {
        this.fn = fn;
        this.parameterNames = parameterNames;
        this.strictArity = strictArity;
    }`
  ],
  [
    `            // reject
            if (params.length > this.parameterNames.length) {`,
    `            // User-defined FEEL functions have an exact arity. Built-ins
            // retain their existing optional/variadic argument behavior.
            if (this.strictArity && params.length !== this.parameterNames.length) {
                return FUNCTION_PARAMETER_MISSMATCH;
            }
            // reject
            if (params.length > this.parameterNames.length) {`
  ],
  [
    `            if (Object.keys(contextOrArgs).some(key => !this.parameterNames.includes(key) && !this.parameterNames.includes(\`...\${key}\`))) {
                return FUNCTION_PARAMETER_MISSMATCH;
            }`,
    `            if (Object.keys(contextOrArgs).some(key => !this.parameterNames.includes(key) && !this.parameterNames.includes(\`...\${key}\`))
                || (this.strictArity && this.parameterNames.some(name => !Object.prototype.hasOwnProperty.call(contextOrArgs, name)))) {
                return FUNCTION_PARAMETER_MISSMATCH;
            }`
  ],
  [
    `            }, parameterNames);
        };
        case 'ContextEntry':`,
    `            }, parameterNames, true);
        };
        case 'ContextEntry':`
  ],
  [
    `function wrapFunction(fn, parameterNames = null) {`,
    `function wrapFunction(fn, parameterNames = null, strictArity = false) {`
  ],
  [
    `    return new FunctionWrapper(fn, parameterNames || parseParameterNames(fn));`,
    `    return new FunctionWrapper(fn, parameterNames || parseParameterNames(fn), strictArity);`
  ],
  [
    `function isDuration(obj) {
    return Duration.isDuration(obj);
}`,
    `function isDuration(obj) {
    return Duration.isDuration(obj);
}
function durationKind(value) {
    if (value.$feelDurationKind) return value.$feelDurationKind;
    const units = value.toObject();
    return Object.prototype.hasOwnProperty.call(units, 'years') || Object.prototype.hasOwnProperty.call(units, 'months')
        ? 'year-month'
        : 'day-time';
}
function scaleDuration(value, factor) {
    if (durationKind(value) === 'year-month') {
        const result = Duration.fromObject({ months: Math.round(value.as('months') * factor) });
        result.$feelDurationKind = 'year-month';
        return result;
    }
    return Duration.fromMillis(value.as('milliseconds') * factor);
}
function divideDurations(left, right) {
    const kind = durationKind(left);
    if (kind !== durationKind(right)) throw new Error('Cannot divide different duration types');
    const unit = kind === 'year-month' ? 'months' : 'milliseconds';
    const divisor = right.as(unit);
    if (divisor === 0) throw new Error('Division by zero');
    return left.as(unit) / divisor;
}
function matchesFeelType(value, type) {
    if (value === null || typeof value === 'undefined') return false;
    const normalized = String(type).trim().replace(/\\s+/g, ' ');
    const lower = normalized.toLowerCase();
    if (lower === 'any') return true;
    if (lower === 'date and time' || lower === 'date_time') return getType(value) === 'date time';
    if (lower === 'number' || lower === 'string' || lower === 'boolean'
        || lower === 'date' || lower === 'time'
        || lower === 'context' || lower === 'function' || lower === 'range') {
        return getType(value) === lower;
    }
    if (lower === 'years and months duration') {
        return isDuration(value) && durationKind(value) === 'year-month';
    }
    if (lower === 'days and time duration') {
        return isDuration(value) && durationKind(value) === 'day-time';
    }
    if (lower.startsWith('range__')) {
        if (getType(value) !== 'range') return false;
        const endpointType = lower.slice('range__'.length).replace(/_/g, ' ');
        return (value.start === null || matchesFeelType(value.start, endpointType))
            && (value.end === null || matchesFeelType(value.end, endpointType));
    }
    const list = /^list\\s*<([\\s\\S]+)>$/i.exec(normalized);
    if (list) return isArray(value) && value.every(entry => entry === null || matchesFeelType(entry, list[1]));
    const contextType = /^context\\s*<([\\s\\S]*)>$/i.exec(normalized);
    if (contextType) {
        if (!isContext(value)) return false;
        const entries = splitFeelTypeEntries(contextType[1]);
        return entries.every(entry => {
            const separator = findTopLevelTypeSeparator(entry);
            if (separator < 0) return false;
            const name = entry.slice(0, separator).trim();
            const entryType = entry.slice(separator + 1).trim();
            return Object.prototype.hasOwnProperty.call(value, name)
                && (value[name] === null || matchesFeelType(value[name], entryType));
        });
    }
    if (/^function\\s*</i.test(normalized)) return getType(value) === 'function';
    return false;
}
function splitFeelTypeEntries(value) {
    const entries = [];
    let depth = 0;
    let start = 0;
    for (let index = 0; index < value.length; index++) {
        if (value[index] === '<') depth++;
        else if (value[index] === '>') depth--;
        else if (value[index] === ',' && depth === 0) {
            entries.push(value.slice(start, index).trim());
            start = index + 1;
        }
    }
    const last = value.slice(start).trim();
    if (last) entries.push(last);
    return entries;
}
function findTopLevelTypeSeparator(value) {
    let depth = 0;
    for (let index = 0; index < value.length; index++) {
        if (value[index] === '<') depth++;
        else if (value[index] === '>') depth--;
        else if (value[index] === ':' && depth === 0) return index;
    }
    return -1;
}`
  ],
  [
    `function duration(opts) {
    if (typeof opts === 'number') {
        return Duration.fromMillis(opts);
    }
    return Duration.fromISO(opts);
}`,
    `function duration(opts) {
    if (typeof opts === 'number') {
        return Duration.fromMillis(opts);
    }
    const text = String(opts);
    const yearMonth = /^(-)?P(?:(\\d+)Y)?(?:(\\d+)M)?$/.exec(text);
    const dayTime = /^(-)?P(?:(\\d+)D)?(?:T(?:(\\d+)H)?(?:(\\d+)M)?(?:(\\d+)(?:\\.(\\d*))?S)?)?$/.exec(text);
    let canonical;
    let kind;
    if (yearMonth && (yearMonth[2] !== undefined || yearMonth[3] !== undefined)) {
        const totalMonths = Number(yearMonth[2] || 0) * 12 + Number(yearMonth[3] || 0);
        const years = Math.floor(totalMonths / 12);
        const months = totalMonths % 12;
        canonical = (yearMonth[1] || '') + 'P'
            + (years ? years + 'Y' : '') + (months || !years ? months + 'M' : '');
        kind = 'year-month';
    }
    else if (dayTime && (dayTime[2] !== undefined || dayTime[3] !== undefined
        || dayTime[4] !== undefined || dayTime[5] !== undefined)
        && (!text.includes('T') || dayTime[3] !== undefined || dayTime[4] !== undefined || dayTime[5] !== undefined)) {
        const totalSeconds = Number(dayTime[2] || 0) * 86400 + Number(dayTime[3] || 0) * 3600
            + Number(dayTime[4] || 0) * 60 + Number(dayTime[5] || 0);
        const days = Math.floor(totalSeconds / 86400);
        const hours = Math.floor(totalSeconds % 86400 / 3600);
        const minutes = Math.floor(totalSeconds % 3600 / 60);
        const seconds = totalSeconds % 60;
        const fraction = (dayTime[6] || '').replace(/0+$/, '');
        canonical = (dayTime[1] || '') + 'P' + (days ? days + 'D' : '');
        if (hours || minutes || seconds || fraction || !days) {
            canonical += 'T' + (hours ? hours + 'H' : '') + (minutes ? minutes + 'M' : '');
            if (seconds || fraction || (!hours && !minutes))
                canonical += seconds + (fraction ? '.' + fraction : '') + 'S';
        }
        kind = 'day-time';
    }
    else {
        throw new Error('invalid FEEL duration');
    }
    const result = Duration.fromISO(canonical);
    if (!result.isValid) throw new Error('invalid FEEL duration');
    result.$feelDurationKind = kind;
    result.$feelLexical = canonical;
    return result;
}`
  ],
  [
    `'years and months duration': fn(function (from, to) {
        return ifValid(to.diff(from, ['years', 'months']));
    }, ['date', 'date'], ['from', 'to']),`,
    `'years and months duration': fn(function (from, to) {
        let start = from;
        let end = to;
        let sign = 1;
        const compareDate = (left, right) =>
            left.year - right.year || left.month - right.month || left.day - right.day;
        if (compareDate(end, start) < 0) {
            start = to;
            end = from;
            sign = -1;
        }
        let months = (end.year - start.year) * 12 + end.month - start.month;
        if (end.day < start.day) months--;
        const result = Duration.fromObject({ months: sign * months });
        result.$feelDurationKind = 'year-month';
        return result;
    }, ['date', 'date'], ['from', 'to']),`
  ],
  [
    `const builtins = {
    // 10.3.4.1 Conversion functions`,
    `const builtins = {
    // 10.3.4.1 Conversion functions
    '__vertexJavaExternal': fn(function (className, methodSignature, args) {
        const signature = methodSignature.split(' ').join('');
        const key = className + '#' + signature;
        const number = value => typeof value === 'number' && Number.isFinite(value)
            ? value
            : FUNCTION_PARAMETER_MISSMATCH;
        if (!Array.isArray(args)) return FUNCTION_PARAMETER_MISSMATCH;
        if (key === 'java.lang.Math#cos(double)' && args.length === 1) {
            const value = number(args[0]);
            return value === FUNCTION_PARAMETER_MISSMATCH ? value : Math.cos(value);
        }
        const maxSignatures = [
            'java.lang.Math#max(double,double)',
            'java.lang.Math#max(float,float)',
            'java.lang.Math#max(int,int)',
            'java.lang.Math#max(long,long)'
        ];
        if (maxSignatures.includes(key) && args.length === 2) {
            const left = number(args[0]);
            const right = number(args[1]);
            return left === FUNCTION_PARAMETER_MISSMATCH || right === FUNCTION_PARAMETER_MISSMATCH
                ? FUNCTION_PARAMETER_MISSMATCH
                : Math.max(left, right);
        }
        if (key === 'java.lang.Short#valueOf(short)' && args.length === 1
            && Number.isInteger(args[0]) && args[0] >= -32768 && args[0] <= 32767) return args[0];
        if (key === 'java.lang.Byte#valueOf(byte)' && args.length === 1
            && Number.isInteger(args[0]) && args[0] >= -128 && args[0] <= 127) return args[0];
        if (key === 'java.lang.String#valueOf(char)' && args.length === 1
            && typeof args[0] === 'string' && Array.from(args[0]).length === 1) return args[0];
        if (key === 'java.lang.Integer#valueOf(java.lang.String)' && args.length === 1
            && typeof args[0] === 'string' && /^[+-]?[0-9]+$/.test(args[0])) {
            const value = Number(args[0]);
            return Number.isInteger(value) && value >= -2147483648 && value <= 2147483647
                ? value
                : FUNCTION_PARAMETER_MISSMATCH;
        }
        if (['java.lang.Float#valueOf(java.lang.String)', 'java.lang.Double#valueOf(java.lang.String)'].includes(key)
            && args.length === 1 && typeof args[0] === 'string') {
            const value = Number(args[0]);
            return Number.isFinite(value) ? value : FUNCTION_PARAMETER_MISSMATCH;
        }
        if (key === 'java.lang.String#format(java.lang.String,[Ljava.lang.Object;)'
            && args.length >= 2 && typeof args[0] === 'string') {
            let index = 1;
            return args[0].replace(/%s/g, () => index < args.length
                ? String(args[index++])
                : '%s');
        }
        return FUNCTION_PARAMETER_MISSMATCH;
    }, ['string', 'string', 'list'], ['class name', 'method signature', 'arguments']),
    'range': fn(function (from) {
        const text = from.trim();
        if (text.length < 4) return FUNCTION_PARAMETER_MISSMATCH;
        const startDelimiter = text[0];
        const endDelimiter = text[text.length - 1];
        if (!['[', '(', ']'].includes(startDelimiter) || ![']', ')', '['].includes(endDelimiter)) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        const body = text.slice(1, -1);
        const separator = body.indexOf('..');
        if (separator < 0) return FUNCTION_PARAMETER_MISSMATCH;
        const startExpression = body.slice(0, separator).trim();
        const endExpression = body.slice(separator + 2).trim();
        const startIncluded = startDelimiter === '[';
        const endIncluded = endDelimiter === ']';
        if ((!startExpression && startIncluded) || (!endExpression && endIncluded)) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        const parseEndpoint = expression => {
            if (!expression) return null;
            if (expression.startsWith('@"') && expression.endsWith('"')) {
                const lexical = JSON.parse(expression.slice(1));
                if (/^-?P/.test(lexical)) return duration(lexical);
                if (/^[0-9]{1,2}:[0-9]{1,2}:[0-9]{1,2}/.test(lexical)) return date(null, lexical);
                return date(lexical);
            }
            if (expression.includes('string(')) return FUNCTION_PARAMETER_MISSMATCH;
            const result = evaluate(expression);
            return result.warnings.length === 0 ? result.value : FUNCTION_PARAMETER_MISSMATCH;
        };
        const start = parseEndpoint(startExpression);
        const end = parseEndpoint(endExpression);
        if (start === FUNCTION_PARAMETER_MISSMATCH || end === FUNCTION_PARAMETER_MISSMATCH) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        if (start !== null && end !== null && getType(start) !== getType(end)) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        if ((start === null && end === null) || (start !== null && end !== null && start > end)) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        return createRange(start, end, startIncluded, endIncluded);
    }, ['string'], ['from']),`
  ],
  [
    `if (args.length === 0) {
            return null;
        }`,
    `if (args.length === 0) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `if (aType === 'range') {
        return [
            [a.start, b.start],
            [a.end, b.end],
            [a['start included'], b['start included']],
            [a['end included'], b['end included']]
        ].every(([a, b]) => a === b);
    }`,
    `if (aType === 'range') {
        if (Boolean(a.$feelUnaryRange) !== Boolean(b.$feelUnaryRange)) return false;
        return equals(a.start, b.start, strict)
            && equals(a.end, b.end, strict)
            && a['start included'] === b['start included']
            && a['end included'] === b['end included'];
    }`
  ],
  [
    `'string join': fn(function (list, delimiter) {
        if (list.some(e => !isString(e) && e !== null)) {
            return null;
        }
        return list.filter(l => l !== null).join(delimiter || '');
    }, ['list', 'string?'], ['list', 'delimiter']),`,
    `'string join': fn(function (list, delimiter) {
        if (list.some(e => !isString(e) && e !== null)) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        return list.filter(l => l !== null).join(delimiter || '');
    }, ['list', 'string?'], ['list', 'delimiter']),`
  ],
  [
    `        if (context === FALSE) {
            return null;
        }
        return context;
    }, 'context', ['...entries']),`,
    `        if (context === FALSE) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        return context;
    }, 'context', ['...entries']),`
  ],
  [
    `function contextPut(context, keys, value) {
    const [key, ...remainingKeys] = keys;
    if (getType(key) !== 'string') {
        return null;
    }
    if (getType(context) === 'nil') {
        return null;
    }
    if (remainingKeys.length) {
        value = contextPut(context[key], remainingKeys, value);
        if (value === null) {
            return null;
        }
    }
    return Object.assign(Object.assign({}, context), { [key]: value });
}`,
    `function contextPut(context, keys, value) {
    if (!Array.isArray(keys) || keys.length === 0) {
        return FUNCTION_PARAMETER_MISSMATCH;
    }
    const [key, ...remainingKeys] = keys;
    if (getType(key) !== 'string' || getType(context) === 'nil') {
        return FUNCTION_PARAMETER_MISSMATCH;
    }
    if (remainingKeys.length) {
        if (getType(context) !== 'context'
            || !Object.prototype.hasOwnProperty.call(context, key)
            || getType(context[key]) !== 'context') {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        value = contextPut(context[key], remainingKeys, value);
        if (value === FUNCTION_PARAMETER_MISSMATCH) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
    }
    return Object.assign(Object.assign({}, context), { [key]: value });
}`
  ],
  [
    `        if (type === 'list') {
            if (arr || optional && typeof obj === 'undefined') {
                return obj;
            }
            else {
                // implicit conversion obj => [ obj ]
                return obj === null ? FALSE : [obj];
            }
        }`,
    `        if (type === 'list') {
            if (arr || optional && typeof obj === 'undefined') {
                return obj;
            }
            else {
                // implicit conversion obj => [ obj ]
                return obj === null || typeof obj === 'undefined' ? FALSE : [obj];
            }
        }`
  ],
  [
    `function offsetted(func, n, scale) {
    const result = func(n * Math.pow(10, scale)) / Math.pow(10, scale);
    return isNaN(result) ? n : result;
}`,
    `function offsetted(func, n, scale) {
    if (!Number.isInteger(scale) || scale < -6111 || scale > 6176) {
        return FUNCTION_PARAMETER_MISSMATCH;
    }
    const result = func(n * Math.pow(10, scale)) / Math.pow(10, scale);
    return isNaN(result) ? n : result;
}`
  ],
  [
    `    'overlaps before': fn(function () {
        throw notImplemented('overlaps before');
    }, ['any?']),
    'overlaps after': fn(function () {
        throw notImplemented('overlaps after');
    }, ['any?']),
    'finishes': fn(function () {
        throw notImplemented('finishes');
    }, ['any?']),
    'finished by': fn(function () {
        throw notImplemented('finished by');
    }, ['any?']),`,
    `    'overlaps before': fn(function (range1, range2) {
        const startsBefore = range1.start < range2.start
            || range1.start === range2.start
                && range1['start included'] && !range2['start included'];
        return startsBefore && !before(range1, range2);
    }, ['range', 'range'], ['range1', 'range2']),
    'overlaps after': fn(function (range1, range2) {
        const startsBefore = range2.start < range1.start
            || range2.start === range1.start
                && range2['start included'] && !range1['start included'];
        return startsBefore && !before(range2, range1);
    }, ['range', 'range'], ['range1', 'range2']),
    'finishes': fn(function (value, range) {
        if (value instanceof Range) {
            return value.end === range.end
                && value['end included'] === range['end included']
                && includesRange(range, value);
        }
        return value === range.end && range['end included'];
    }, ['any', 'range'], ['value', 'range']),
    'finished by': fn(function (range, value) {
        if (value instanceof Range) {
            return value.end === range.end
                && value['end included'] === range['end included']
                && includesRange(range, value);
        }
        return value === range.end && range['end included'];
    }, ['range', 'any'], ['range', 'value']),`
  ],
  [
    `    'during': fn(function () {
        throw notImplemented('during');
    }, ['any?']),
    'starts': fn(function () {
        throw notImplemented('starts');
    }, ['any?']),
    'started by': fn(function () {
        throw notImplemented('started by');
    }, ['any?']),
    'coincides': fn(function () {
        throw notImplemented('coincides');
    }, ['any?']),`,
    `    'during': fn(function (value, range) {
        return includesRange(range, value);
    }, ['any', 'range'], ['value', 'range']),
    'starts': fn(function (value, range) {
        if (value instanceof Range) {
            return value.start === range.start
                && value['start included'] === range['start included']
                && includesRange(range, value);
        }
        return value === range.start && range['start included'];
    }, ['any', 'range'], ['value', 'range']),
    'started by': fn(function (range, value) {
        if (value instanceof Range) {
            return value.start === range.start
                && value['start included'] === range['start included']
                && includesRange(range, value);
        }
        return value === range.start && range['start included'];
    }, ['range', 'any'], ['range', 'value']),
    'coincides': fn(function (value1, value2) {
        return equals(value1, value2) === true;
    }, ['any', 'any'], ['value1', 'value2']),`
  ],
  [
    `    'day of year': fn(function (date) {
        return date.ordinal;
    }, ['date time'], ['date']),
    'day of week': fn(function (date) {
        return date.weekdayLong;
    }, ['date time'], ['date']),
    'month of year': fn(function (date) {
        return date.monthLong;
    }, ['date time'], ['date']),
    'week of year': fn(function (date) {
        return date.weekNumber;
    }, ['date time'], ['date']),`,
    `    'day of year': fn(function (date) {
        return ['date', 'date time'].includes(getType(date))
            ? date.ordinal
            : FUNCTION_PARAMETER_MISSMATCH;
    }, ['any'], ['date']),
    'day of week': fn(function (date) {
        return ['date', 'date time'].includes(getType(date))
            ? date.setLocale('en').weekdayLong
            : FUNCTION_PARAMETER_MISSMATCH;
    }, ['any'], ['date']),
    'month of year': fn(function (date) {
        return ['date', 'date time'].includes(getType(date))
            ? date.setLocale('en').monthLong
            : FUNCTION_PARAMETER_MISSMATCH;
    }, ['any'], ['date']),
    'week of year': fn(function (date) {
        return ['date', 'date time'].includes(getType(date))
            ? date.weekNumber
            : FUNCTION_PARAMETER_MISSMATCH;
    }, ['any'], ['date']),`
  ],
  [
    `    'today': fn(function () {
        return date().startOf('day');
    }, [], []),`,
    `    'today': fn(function () {
        return date().setZone('utc', { keepLocalTime: true }).startOf('day');
    }, [], []),`
  ],
  [
    `    'not': fn(function (negand) {
        return isType(negand, 'boolean') ? !negand : null;
    }, ['any'], ['negand']),`,
    `    'not': fn(function (negand) {
        if (negand === null) return null;
        return isType(negand, 'boolean') ? !negand : FUNCTION_PARAMETER_MISSMATCH;
    }, ['any'], ['negand']),`
  ],
  [
    `    'stddev': listFn(function (...list) {
        if (list.length < 2) {
            return null;
        }
        return stddev(list);
    }, 'number', ['...list']),`,
    `    'stddev': listFn(function (...list) {
        if (list.length < 2) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        return stddev(list);
    }, 'number', ['...list']),`
  ],
  [
    `    'floor': fn(function (n, scale = 0) {
        if (scale === null) {
            return null;
        }
        const adjust = Math.pow(10, scale);
        return Math.floor(n * adjust) / adjust;
    }, ['number', 'number?'], ['n', 'scale']),
    'ceiling': fn(function (n, scale = 0) {
        if (scale === null) {
            return null;
        }
        const adjust = Math.pow(10, scale);
        return Math.ceil(n * adjust) / adjust;
    }, ['number', 'number?'], ['n', 'scale']),`,
    `    'floor': fn(function (n, scale = 0) {
        if (scale === null) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        const adjust = Math.pow(10, scale);
        return Math.floor(n * adjust) / adjust;
    }, ['number', 'number?'], ['n', 'scale']),
    'ceiling': fn(function (n, scale = 0) {
        if (scale === null) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        const adjust = Math.pow(10, scale);
        return Math.ceil(n * adjust) / adjust;
    }, ['number', 'number?'], ['n', 'scale']),`
  ],
  [
    `        case 'Context': return (context) => {
            return args.slice(1, -1).reduce((obj, arg) => {
                const [key, value] = arg(Object.assign(Object.assign({}, context), obj));
                return Object.assign(Object.assign({}, obj), { [key]: value });
            }, {});
        };`,
    `        case 'Context': return (context) => {
            return args.slice(1, -1).reduce((obj, arg) => {
                const [key, value] = arg(Object.assign(Object.assign({}, context), obj));
                if (Object.prototype.hasOwnProperty.call(obj, key))
                    throw new Error(\`Duplicate FEEL context key '\${key}'\`);
                return Object.assign(Object.assign({}, obj), { [key]: value });
            }, {});
        };`
  ],
  [
    `            if (operator === 'between') {
                const start = args[2](context);
                const end = args[4](context);
                if (start === null || end === null) {
                    return null;
                }
                return createRange(start, end).includes(args[0](context));
            }`,
    `            if (operator === 'between') {
                const value = args[0](context);
                const start = args[2](context);
                const end = args[4](context);
                if (value === null || start === null || end === null)
                    throw new Error('FEEL between operands must not be null');
                return createRange(start, end).includes(value);
            }`
  ],
  [
    `        case 'Interval': return tag((context) => {
            const left = args[1](context);
            const right = args[3](context);
            const startIncluded = left !== null && args[0] === '[';
            const endIncluded = right !== null && args[4] === ']';
            return createRange(left, right, startIncluded, endIncluded);
        }, 'test');`,
    `        case 'Interval': return tag((context) => {
            const left = args[1](context);
            const right = args[3](context);
            if (left === null && args[0] === '[' || right === null && args[4] === ']')
                throw new Error('A null FEEL interval endpoint cannot be included');
            const startIncluded = left !== null && args[0] === '[';
            const endIncluded = right !== null && args[4] === ']';
            return createRange(left, right, startIncluded, endIncluded);
        }, 'test');`
  ],
  [
    `function compareIn(value, tests) {
    if (!isArray(tests)) {
        if (getType(tests) === 'nil') {
            return null;
        }
        tests = [tests];
    }
    return tests.some(test => compareValue(test, value));
}`,
    `function compareIn(value, tests) {
    if (value === null) throw new Error('The FEEL in operand must not be null');
    if (!isArray(tests)) {
        if (getType(tests) === 'nil') return null;
        tests = [tests];
    }
    const results = tests.map(test => compareValue(test, value));
    return results.some(result => result === true)
        ? true
        : results.some(result => result === null) ? null : false;
}`
  ],
  [
    `    if (test instanceof Range) {
        return test.includes(value);
    }`,
    `    if (test instanceof Range) {
        if ((test.start === null || test.end === null) && !test.$feelUnaryRange) return null;
        return test.includes(value);
    }`
  ],
  [
    `    return equals(test, value);
}`,
    `    const result = equals(test, value);
    return result === null ? false : result;
}`
  ],
  [
    `const FALSE = {};`,
    `const FALSE = {};
const SUPPRESS_MISSING_PROPERTY_WARNING = Symbol('suppress missing FEEL property warning');`
  ],
  [
    `        case 'PathName': return tag((context) => {
            const name = args.join(' ');
            const contextValue = getFromContext(name, context);
            if (typeof contextValue !== 'undefined') {
                return contextValue;
            }
            if (isContext(context)) {`,
    `        case 'PathName': return tag((context) => {
            const name = args.join(' ');
            const contextValue = getFromContext(name, context);
            if (typeof contextValue !== 'undefined') {
                return contextValue;
            }
            if (context && context[SUPPRESS_MISSING_PROPERTY_WARNING]) return null;
            if (isContext(context)) {`
  ],
  [
    `        case 'VariableName': return tag((context) => {
            const name = args.join(' ');
            const contextValue = getFromContext(name, context);
            if (typeof contextValue !== 'undefined') {
                return contextValue;
            }
            const builtin = getBuiltin(name);`,
    `        case 'VariableName': return tag((context) => {
            const name = args.join(' ');
            const contextValue = getFromContext(name, context);
            if (typeof contextValue !== 'undefined') {
                return contextValue;
            }
            if (context && context[SUPPRESS_MISSING_PROPERTY_WARNING]) return null;
            const builtin = getBuiltin(name);`
  ],
  [
    `            if (isArray(pathTarget)) {
                return pathTarget.map(value => pathProp(value));
            }`,
    `            if (isArray(pathTarget)) {
                return pathTarget.map(value => pathProp(isContext(value)
                    ? Object.assign({}, value, { [SUPPRESS_MISSING_PROPERTY_WARNING]: true })
                    : value));
            }`
  ],
  [
    `                    const iterationContext = Object.assign(Object.assign(Object.assign({}, context), { item: el }), el);`,
    `                    const iterationContext = Object.assign(Object.assign(Object.assign(Object.assign({}, context), { item: el }), el), { [SUPPRESS_MISSING_PROPERTY_WARNING]: true });`
  ],
  [
    `                    if (idx === true) {
                        return target;
                    }`,
    `                    if (idx === true) {
                        return filterTarget;
                    }`
  ],
  [
    `    'product': listFn(function (...list) {
        if (list.length === 0) {
            return null;
        }
        return list.reduce((result, n) => {
            return result * n;
        }, 1);
    }, 'number', ['...list']),`,
    `    'product': listFn(function (...list) {
        if (list.length === 0) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }
        return list.reduce((result, n) => {
            return result * n;
        }, 1);
    }, 'number', ['...list']),`
  ],
  [
    `    'decimal': fn(function (n, scale) {
        if (n === null || scale === null)
            return null;
        return offsetted(bankersRound, n, scale);
    }, ['number', 'number'], ['n', 'scale']),`,
    `    'decimal': fn(function (n, scale) {
        if (n === null || scale === null)
            return null;
        return offsetted(bankersRound, n, Math.trunc(scale));
    }, ['number', 'number'], ['n', 'scale']),`
  ],
  [
    `    'is': fn(function (value1, value2) {
        if (typeof value1 === 'undefined' || typeof value2 === 'undefined') {
            return false;
        }
        return equals(value1, value2, true);
    }, ['any?', 'any?'], ['value1', 'value2']),`,
    `    'is': fn(function (value1, value2) {
        if (typeof value1 === 'undefined' || typeof value2 === 'undefined') {
            return false;
        }
        return equals(value1, value2, true) === true;
    }, ['any?', 'any?'], ['value1', 'value2']),`
  ],
  [
    `        if (!temporalTypes.includes(bType)) {
            return null;
        }
        if (aType === 'time' && bType !== 'time') {`,
    `        if (!temporalTypes.includes(bType)) {
            return null;
        }
        if (strict && aType !== bType) return false;
        if (aType === 'time' && bType !== 'time') {`
  ],
  [
    `            // unary expression (-b)
            if (args.length === 2) {
                const [op, value] = args;
                return tag((context) => {
                    return op(context)(() => 0, value);
                }, value.type);
            }`,
    `            // unary expression (-b)
            if (args.length === 2) {
                const [op, value] = args;
                return tag((context) => {
                    const evaluated = value(context);
                    if (isDuration(evaluated)) return scaleDuration(evaluated, -1);
                    return op(context)(() => 0, () => evaluated);
                }, value.type);
            }`
  ],
  [
    `    if (aType === 'duration') {
        // years and months duration -> months`,
    `    if (aType === 'duration') {
        if (durationKind(a) !== durationKind(b)) return null;
        // years and months duration -> months`
  ],
  [
    `        case 'CompareOp': return tag(() => {
            switch (node.input) {
                case '>': return (b) => createRange(b, null, false, false);
                case '>=': return (b) => createRange(b, null, true, false);
                case '<': return (b) => createRange(null, b, false, false);
                case '<=': return (b) => createRange(null, b, false, true);
                case '=': return (b) => (a) => equals(a, b);
                case '!=': return (b) => (a) => !equals(a, b);
            }
        }, 'test');`,
    `        case 'CompareOp': return tag(() => {
            const equalityTest = (operator, expected) => {
                const test = (actual) => {
                    const result = equals(actual, expected);
                    if (result === null) throw new Error('Cannot compare incompatible FEEL types');
                    return operator === '=' ? result : !result;
                };
                test.$feelComparisonOperator = operator;
                test.$feelComparisonValue = expected;
                return test;
            };
            const unaryRange = range => Object.assign(range, { $feelUnaryRange: true });
            switch (node.input) {
                case '>': return (b) => unaryRange(createRange(b, null, false, false));
                case '>=': return (b) => unaryRange(createRange(b, null, true, false));
                case '<': return (b) => unaryRange(createRange(null, b, false, false));
                case '<=': return (b) => unaryRange(createRange(null, b, false, true));
                case '=': return (b) => equalityTest('=', b);
                case '!=': return (b) => equalityTest('!=', b);
            }
        }, 'test');`
  ],
  [
    `    if (aType !== bType) {
        return null;
    }`,
    `    if (aType !== bType) {
        if ((a && a.$feelComparisonOperator) || (b && b.$feelComparisonOperator)) return false;
        return null;
    }
    if (aType === 'function' && (a.$feelComparisonOperator || b.$feelComparisonOperator)) {
        return Boolean(a.$feelComparisonOperator) && Boolean(b.$feelComparisonOperator)
            && a.$feelComparisonOperator === b.$feelComparisonOperator
            && equals(a.$feelComparisonValue, b.$feelComparisonValue, strict) === true;
    }`
  ],
  [
    `function listReplace(list, matcher, newItem) {
    if (isNumber(matcher)) {
        return [...list.slice(0, matcher - 1), newItem, ...list.slice(matcher)];
    }
    return list.map((item, _idx) => {
        if (matcher.invoke([item, newItem])) {
            return newItem;
        }
        else {
            return item;
        }
    });
}` ,
    `function listReplace(list, matcher, newItem) {
    if (isNumber(matcher)) {
        if (!Number.isFinite(matcher))
            return FUNCTION_PARAMETER_MISSMATCH;
        matcher = Math.trunc(matcher);
        if (matcher === 0 || Math.abs(matcher) > list.length)
            return FUNCTION_PARAMETER_MISSMATCH;
        const index = matcher > 0 ? matcher - 1 : list.length + matcher;
        const result = list.slice();
        result[index] = newItem;
        return result;
    }
    if (!(matcher instanceof FunctionWrapper) || matcher.parameterNames.length !== 2)
        return FUNCTION_PARAMETER_MISSMATCH;
    const result = [];
    for (const item of list) {
        const matched = matcher.invoke([item, newItem]);
        if (matched === FUNCTION_PARAMETER_MISSMATCH || typeof matched !== 'boolean')
            return FUNCTION_PARAMETER_MISSMATCH;
        result.push(matched ? newItem : item);
    }
    return result;
}`
  ],
  [
    `'list replace': fn(function (list, position, newItem, match) {
        const matcher = position || match;
        if (!['number', 'function'].includes(getType(matcher))) {
            return null;
        }
        return listReplace(list, position || match, newItem);
    }, ['list', 'any?', 'any', 'function?'], ['list', 'position', 'newItem', 'match']),`,
    `'list replace': fn(function (list, position, newItem, match) {
        const matcher = typeof position === 'undefined' ? match : position;
        return listReplace(list, matcher, newItem);
    }, ['list', 'any?', 'any', 'function?'], ['list', 'position', 'newItem', 'match']),`
  ],
  [
    `if (groupingSeparator) {
            from = from.split(groupingSeparator).join('');
        }
        if (decimalSeparator && decimalSeparator !== '.') {`,
    `if (groupingSeparator && groupingSeparator === decimalSeparator) return FUNCTION_PARAMETER_MISSMATCH;
        if (decimalSeparator && decimalSeparator !== '.' && decimalSeparator !== ',') return FUNCTION_PARAMETER_MISSMATCH;
        if (groupingSeparator) {
            from = from.split(groupingSeparator).join('');
        }
        if (decimalSeparator && decimalSeparator !== '.') {`
  ],
  [
    `if (isNaN(number)) {
            return null;
        }`,
    `if (isNaN(number)) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `'abs': fn(function (n) {
        if (typeof n !== 'number') {
            return null;
        }
        return Math.abs(n);
    }, ['number'], ['n']),`,
    `'abs': fn(function (n) {
        if (typeof n === 'number') return Math.abs(n);
        if (isDuration(n)) {
            const result = n.valueOf() < 0 ? n.negate() : n;
            result.$feelDurationKind = durationKind(n);
            return result;
        }
        return FUNCTION_PARAMETER_MISSMATCH;
    }, ['any'], ['n']),`
  ],
  [
    `if (!divisor) {
            return null;
        }`,
    `if (!divisor) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `if (number < 0) {
            return null;
        }`,
    `if (number < 0) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `if (number <= 0) {
            return null;
        }`,
    `if (number <= 0) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `if (!args.every(arg => tester(arg) !== FALSE)) {
            return null;
        }`,
    `if (!args.every(arg => tester(arg) !== FALSE)) {
            return FUNCTION_PARAMETER_MISSMATCH;
    }`
  ],
  [
    `'all': listFn(function (...list) {
        let nonBool = false;
        for (const o of list) {
            if (o === false) {
                return false;
            }
            if (typeof o !== 'boolean') {
                nonBool = true;
            }
        }
        return nonBool ? null : true;
    }, 'any?', ['...list']),`,
    `'all': listFn(function (...list) {
        if (list.length === 1 && list[0] === null) return FUNCTION_PARAMETER_MISSMATCH;
        let containsNull = false;
        for (const value of list) {
            if (value === false) return false;
            if (value === null) containsNull = true;
            else if (typeof value !== 'boolean') return FUNCTION_PARAMETER_MISSMATCH;
        }
        return containsNull ? null : true;
    }, 'any?', ['...list']),`
  ],
  [
    `'any': listFn(function (...list) {
        let nonBool = false;
        for (const o of list) {
            if (o === true) {
                return true;
            }
            if (typeof o !== 'boolean') {
                nonBool = true;
            }
        }
        return nonBool ? null : false;
    }, 'any?', ['...list']),`,
    `'any': listFn(function (...list) {
        if (list.length === 1 && list[0] === null) return FUNCTION_PARAMETER_MISSMATCH;
        let containsNull = false;
        for (const value of list) {
            if (value === true) return true;
            if (value === null) containsNull = true;
            else if (typeof value !== 'boolean') return FUNCTION_PARAMETER_MISSMATCH;
        }
        return containsNull ? null : false;
    }, 'any?', ['...list']),`
  ],
  [
    `if (!convertedArgs) {
            return null;
        }`,
    `if (!convertedArgs) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `if (offset) {
            throw notImplemented('time(..., offset)');
        }`,
    `let zone = null;
        if (offset) {
            if (!isDuration(offset)) return null;
            const minutes = offset.as('minutes');
            if (!Number.isFinite(minutes) || Math.abs(minutes) > 14 * 60) return null;
            zone = FixedOffsetZone.instance(minutes);
        }`
  ],
  [
    `t = date().set({
                hour,`,
    `t = date().setZone(zone || SystemZone.instance).set({
                hour,`
  ],
  [
    `const dLocal = d.toLocal();`,
    `const dLocal = d;`
  ],
  [
    `case 'Type': return args[0];`,
    `case 'Type': return (_context) => node.input;`
  ],
  [
    `case 'SpecialType': throw notImplemented('SpecialType');
        case 'InstanceOfExpression': return tag((context) => {
            const a = args[0](context);
            const b = args[3](context);
            return a instanceof b;
        }, 'test');`,
    `case 'SpecialType': return node.input;
        case 'InstanceOfExpression': return tag((context) => {
            const value = args[0](context);
            const type = args[3](context);
            return matchesFeelType(value, type);
        }, 'test');`
  ],
  [
    `const temporal = ['date', 'time', 'date time', 'duration'];
                if (temporal.includes(leftType)) {
                    if (!temporal.includes(rightType)) {
                        interpreterContext.addWarning(node, 'INVALID_TYPE', {
                            template: \`Can't \${opName} {right} to {left}\`,
                            values: {
                                left,
                                right
                            }
                        });
                        return null;
                    }
                }
                else if (leftType !== rightType || !types.includes(leftType)) {`,
    `const compatible = leftType === rightType
                    ? types.includes(leftType)
                    : types.includes(\`\${leftType}:\${rightType}\`);
                if (!compatible) {`
  ],
  [
    `else if (isDuration(a) && isDuration(b)) {
                        return a.plus(b);
                    }`,
    `else if (isDuration(a) && isDuration(b)) {
                        if (durationKind(a) !== durationKind(b)) throw new Error('Cannot add different duration types');
                        return a.plus(b);
                    }`
  ],
  [
    `else if (isDateTime(a) && isDuration(b)) {
                        return a.plus(b);
                    }`,
    `else if (isDateTime(a) && isDuration(b)) {
                        const result = a.plus(b);
                        return isType(a, 'date') ? result.startOf('day') : result;
                    }`
  ],
  [
    `if (isType(a, 'time') && isDuration(b)) {
                        return a.plus(b).set({`,
    `if (isType(a, 'time') && isDuration(b)) {
                        if (durationKind(b) !== 'day-time') throw new Error('Cannot add a year-month duration to a time');
                        return a.plus(b).set({`
  ],
  [
    `}, 'add', ['string', 'number', 'date', 'time', 'duration', 'date time']);`,
    `}, 'add', ['string', 'number', 'duration', 'date:duration', 'duration:date', 'time:duration', 'duration:time', 'date time:duration', 'duration:date time']);`
  ],
  [
    `if (isType(a, 'time') && isDuration(b)) {
                        return a.minus(b).set({`,
    `if (isType(a, 'time') && isDuration(b)) {
                        if (durationKind(b) !== 'day-time') throw new Error('Cannot subtract a year-month duration from a time');
                        return a.minus(b).set({`
  ],
  [
    `else if (isDuration(a) && isDuration(b)) {
                        return a.minus(b);
                    }`,
    `else if (isDuration(a) && isDuration(b)) {
                        if (durationKind(a) !== durationKind(b)) throw new Error('Cannot subtract different duration types');
                        return a.minus(b);
                    }`
  ],
  [
    `else if (isDateTime(a) && isDuration(b)) {
                        return a.minus(b);
                    }`,
    `else if (isDateTime(a) && isDuration(b)) {
                        const result = a.minus(b);
                        return isType(a, 'date') ? result.startOf('day') : result;
                    }`
  ],
  [
    `else if (isDateTime(a) && isDateTime(b)) {
                        return a.diff(b);
                    }`,
    `else if (isDateTime(a) && isDateTime(b)) {
                        if (isType(a, 'date time') && isType(b, 'date time')) {
                            const aHasZone = a.zone !== SystemZone.instance;
                            const bHasZone = b.zone !== SystemZone.instance;
                            if (aHasZone !== bHasZone) throw new Error('Both date-time operands must either have a timezone or have no timezone');
                        }
                        else if (isType(a, 'date') !== isType(b, 'date')) {
                            const dateTime = isType(a, 'date time') ? a : b;
                            if (dateTime.zone === SystemZone.instance) throw new Error('A date-time subtracted with a date must have a timezone');
                        }
                        return a.diff(b);
                    }`
  ],
  [
    `}, 'subtract', ['number', 'date', 'time', 'duration', 'date time']);
                case '*': return nullable((a, b) => a * b, 'multiply', ['number']);
                case '/': return nullable((a, b) => !b ? null : a / b, 'divide', ['number']);`,
    `}, 'subtract', ['number', 'date', 'time', 'date time', 'duration', 'date:date time', 'date time:date', 'date:duration', 'time:duration', 'date time:duration']);
                case '*': return nullable((a, b) => {
                    if (isDuration(a)) return scaleDuration(a, b);
                    if (isDuration(b)) return scaleDuration(b, a);
                    return a * b;
                }, 'multiply', ['number', 'number:duration', 'duration:number']);
                case '/': return nullable((a, b) => {
                    if (isDuration(a) && isDuration(b)) return divideDurations(a, b);
                    if (isDuration(a)) {
                        if (b === 0) throw new Error('Division by zero');
                        return scaleDuration(a, 1 / b);
                    }
                    if (b === 0) return null;
                    return a / b;
                }, 'divide', ['number', 'duration:number', 'duration']);`
  ],
  [
    `function getFromContext(name, context) {
    if (['nil', 'boolean', 'number', 'string'].includes(getType(context))) {`,
    `function getFromContext(name, context) {
    const type = getType(context);
    const property = normalizeContextKey(name);
    if (type === 'function' && context.$feelComparisonOperator === '=') {
        const comparisonProperties = {
            start: context.$feelComparisonValue,
            end: context.$feelComparisonValue,
            'start included': true,
            'end included': true
        };
        if (property in comparisonProperties) return comparisonProperties[property];
    }
    if (['date', 'time', 'date time'].includes(type)) {
        const dateTimeProperties = {
            year: context.year,
            month: context.month,
            day: context.day,
            hour: context.hour,
            minute: context.minute,
            second: context.second + context.millisecond / 1000,
            'time offset': context.zone === SystemZone.instance ? null : Duration.fromObject({ minutes: context.offset }),
            timezone: context.zone && context.zone.type === 'iana' ? context.zoneName : null
        };
        if (property in dateTimeProperties) return dateTimeProperties[property];
    }
    if (type === 'duration') {
        const yearMonth = durationKind(context) === 'year-month';
        const normalized = yearMonth
            ? context.shiftTo('years', 'months').toObject()
            : context.shiftTo('days', 'hours', 'minutes', 'seconds').toObject();
        const durationProperties = yearMonth
            ? { years: normalized.years || 0, months: normalized.months || 0 }
            : {
                days: normalized.days || 0,
                hours: normalized.hours || 0,
                minutes: normalized.minutes || 0,
                seconds: normalized.seconds || 0
            };
        if (property in durationProperties) return durationProperties[property];
        return undefined;
    }
    if (['nil', 'boolean', 'number', 'string'].includes(type)) {`
  ],
  [
    `function buildFlags(flags, defaultFlags) {
    const unsupportedFlags = flags.replace(/[smix]/g, '');
    if (unsupportedFlags) {
        throw new Error('illegal flags: ' + unsupportedFlags);
    }
    // we don't implement the <x> flag
    if (/x/.test(flags)) {
        throw notImplemented('matches <x> flag');
    }
    return flags + defaultFlags;
}` ,
    `function normalizeExtendedRegexp(pattern) {
    let result = '';
    let inClass = false;
    for (let index = 0; index < pattern.length; index++) {
        const character = pattern[index];
        if (character === '\\\\' && index + 1 < pattern.length) {
            if (/\\s/.test(pattern[index + 1])) {
                result += '\\\\';
                while (index + 1 < pattern.length && /\\s/.test(pattern[index + 1])) index++;
            }
            else {
                result += '\\\\' + pattern[++index];
            }
            continue;
        }
        if (!inClass && character === '#') {
            while (index + 1 < pattern.length && !/[\\r\\n]/.test(pattern[index + 1])) index++;
            continue;
        }
        if (character === '[') inClass = true;
        else if (character === ']') inClass = false;
        if (!inClass && /\\s/.test(character)) continue;
        result += character;
    }
    return result;
}
function normalizeXmlSchemaRegexp(pattern, flags) {
    let normalized = flags.includes('x') ? normalizeExtendedRegexp(pattern) : pattern;
    normalized = normalized.split('\\\\p{IsBasicLatin}').join('[\\\\u0000-\\\\u007F]');
    normalized = normalized.replace(/\\[([^\\[\\]]+)-\\[([^\\[\\]]+)\\]\\]/g,
        (_match, base, subtraction) => \`(?:(?![\${subtraction}])[\${base}])\`);
    return normalized;
}
function buildFlags(flags, defaultFlags) {
    const unsupportedFlags = flags.replace(/[smix]/g, '');
    if (unsupportedFlags) throw new Error('illegal flags: ' + unsupportedFlags);
    return [...new Set((flags.replace(/x/g, '') + defaultFlags).split(''))].join('');
}`
  ],
  [
    `function createRegexp(pattern, flags, defaultFlags = '') {
    try {
        return new RegExp(pattern, 'u' + buildFlags(flags, defaultFlags));
    }
    catch (_err) {
        if (isNotImplemented(_err)) {
            throw _err;
        }
    }
    return null;
}` ,
    `function createRegexp(pattern, flags, defaultFlags = '') {
    const normalized = normalizeXmlSchemaRegexp(pattern, flags);
    if (/\\[[^\\]]*\\\\[0-9]/.test(normalized))
        throw new Error('invalid numeric escape inside character class');
    try {
        return new RegExp(normalized, 'u' + buildFlags(flags, defaultFlags));
    }
    catch (error) {
        throw new Error(\`invalid regular expression: \${error.message}\`);
    }
}`
  ],
  [
    `t = date().setZone(zone || SystemZone.instance).set({
                hour,
                minute,
                second
            }).set({
                year: 1900,
                month: 1,
                day: 1,
                millisecond: 0
            });`,
    `t = date().setZone(zone || SystemZone.instance).set({
                hour,
                minute,
                second: Math.trunc(second),
                millisecond: Math.round((second - Math.trunc(second)) * 1000)
            }).set({
                year: 1900,
                month: 1,
                day: 1
            });`
  ],
  [
    `function extractValue(context, prop, _target) {
    const target = _target(context);
    if (['list', 'range'].includes(getType(target))) {
        return target.map(t => ({ [prop]: t }));
    }
    return null;
}`,
    `function extractValue(context, prop, _target) {
    const target = _target(context);
    const type = getType(target);
    if (type === 'range') {
        const endpointType = getType(target.start);
        if (endpointType !== 'number' && endpointType !== 'date')
            throw new Error(\`Unsupported FEEL iteration range type \${endpointType}\`);
        return target.map(t => ({ [prop]: t }));
    }
    if (type === 'list') {
        if (target.some(item => item instanceof Range))
            throw new Error('A FEEL range is not a valid list iteration item');
        return target.map(t => ({ [prop]: t }));
    }
    return null;
}`
  ],
  [
    `function createDateTimeRange(start, end, startIncluded, endIncluded) {
    const map = noopMap();
    const includes = anyIncludes(start, end, startIncluded, endIncluded);
    return new Range({
        start,
        end,
        'start included': startIncluded,
        'end included': endIncluded,
        map,
        includes
    });
}`,
    `function dateMap(start, end, startIncluded, endIncluded) {
    const direction = start.toMillis() > end.toMillis() ? -1 : 1;
    return (fn) => {
        const result = [];
        let current = startIncluded ? start : start.plus({ days: direction });
        const final = endIncluded ? end : end.minus({ days: direction });
        while (direction > 0 ? current.toMillis() <= final.toMillis() : current.toMillis() >= final.toMillis()) {
            current.$feelTemporalType = 'date';
            current.$feelLexical = current.toISODate();
            result.push(fn(current));
            current = current.plus({ days: direction });
        }
        return result;
    };
}
function createDateTimeRange(start, end, startIncluded, endIncluded) {
    const map = start !== null && end !== null && isTyped('date', [start, end])
        ? dateMap(start, end, startIncluded, endIncluded)
        : noopMap();
    const includes = anyIncludes(start, end, startIncluded, endIncluded);
    return new Range({
        start,
        end,
        'start included': startIncluded,
        'end included': endIncluded,
        map,
        includes
    });
}`
  ],
  [
    `            if (left === null && args[0] === '[' || right === null && args[4] === ']')
                throw new Error('A null FEEL interval endpoint cannot be included');
            const startIncluded = left !== null && args[0] === '[';`,
    `            if (left === null && args[0] === '[' || right === null && args[4] === ']')
                throw new Error('A null FEEL interval endpoint cannot be included');
            if (left !== null && right !== null && left > right)
                throw new Error('A FEEL interval start must not be greater than its end');
            const startIncluded = left !== null && args[0] === '[';`
  ]
];

for (const [original, replacement] of replacements) {
  const first = source.indexOf(original);
  if (first < 0 || source.indexOf(original, first + original.length) >= 0) {
    throw new Error(`Pinned feelin source no longer matches strict patch: ${original.slice(0, 120)}`);
  }
  source = source.replace(original, replacement);
}

writeFileSync(targetPath, source, 'utf8');
