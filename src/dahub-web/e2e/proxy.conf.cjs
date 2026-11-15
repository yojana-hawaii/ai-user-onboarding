// Isolated E2E network only. Daily development retains its verified HTTPS proxy.
module.exports = { '/api/**': { target: 'http://e2e-api:5080', secure: true } };
