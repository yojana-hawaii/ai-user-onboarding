const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const { dirname, join } = require('node:path');
const { describe, test } = require('node:test');

for (const [name, modulePath] of [
  ['application', 'picomatch'],
  [
    'npm toolchain',
    '/usr/local/lib/node_modules/npm/node_modules/tinyglobby/node_modules/picomatch',
  ],
]) {
  describe(name, () => {
    const target = require.resolve(modulePath);
    const picomatch = require(target);

    test('uses the reviewed, pinned patched distribution', () => {
      const pkg = require(join(dirname(target), 'package.json'));
      assert.equal(pkg.name, '@ryotsu/picomatch');
      assert.equal(pkg.version, '4.0.7-a');
    });

    test('single-branch repeated wildcard attacks finish in an isolated bounded process', () => {
      const result = spawnSync(
        process.execPath,
        [
          '-e',
          `
        const assert = require('node:assert/strict');
        const pm = require(${JSON.stringify(target)});
        const cases = [
          ['+(*a)', 'a'.repeat(4096) + '!'],
          ['*(*a)', 'a'.repeat(4096) + '!'],
          ['+(a*)', 'a'.repeat(4096) + '/'],
          ['*(a*)', 'a'.repeat(4096) + '/'],
          ['+(*)', 'a'.repeat(4096) + '/'],
          ['*(*)', 'a'.repeat(4096) + '/'],
          ['+(*)b', 'a'.repeat(4096) + '!'],
          ['+(*a)c', 'a'.repeat(4096) + '!'],
          ['+(a*b)', 'ab'.repeat(128) + '!'],
          ['+(**a)', 'a'.repeat(4096) + '!'],
          ['+(@(*a))', 'a'.repeat(4096) + '!'],
          ['+(?(*a))', 'a'.repeat(4096) + '!']
        ];
        for (const options of [{}, { windows: true }, { capture: true }]) {
          for (const [pattern, input] of cases) {
            assert.equal(pm.isMatch(input, pattern, options), false, pattern);
          }
        }
      `,
        ],
        { encoding: 'utf8', timeout: 5000, maxBuffer: 1024 * 1024, windowsHide: true },
      );
      assert.ifError(result.error);
      assert.equal(result.status, 0, result.stderr);
    });

    test('preserves useful repeated wildcard matching', () => {
      assert(picomatch.isMatch('abc', '+(*)c'));
      assert(picomatch.isMatch('axbayb', '+(a*b)'));
      assert(picomatch.isMatch('foo.js', '*(*.js)'));
      assert(!picomatch.isMatch('axbay', '+(a*b)'));
      assert(picomatch.isMatch('*a*a', '+("*a")'));
      assert(picomatch.isMatch('a*ba*b', '+(a\\*b)'));
    });

    test('fails closed for complex single-branch wildcard forms', () => {
      for (const pattern of ['+(?(*a))', '+(*a/b)', '+(*{a,b})']) {
        assert(picomatch.isMatch(pattern, pattern));
        assert(!picomatch.isMatch('aaaaaaaa!', pattern));
      }
    });

    test('retains glob selection, ignore, scan, parse and POSIX APIs used by tooling', () => {
      const match = picomatch(['**/*.ts', '**/*.html'], {
        dot: true,
        ignore: '**/node_modules/**',
      });
      assert(match('src/app/app.ts'));
      assert(match('src/.internal/app.html'));
      assert(!match('node_modules/example/index.ts'));
      assert(!match('src/image.png'));
      assert.equal(picomatch.scan('src/**/*.ts').base, 'src');
      assert.equal(typeof picomatch.parse('**/*.ts').output, 'string');
      assert(picomatch.makeRe('**/*.ts').test('src/app.ts'));
      assert(require(join(dirname(target), 'posix.js'))('src/*.ts')('src/app.ts'));
    });
  });
}
