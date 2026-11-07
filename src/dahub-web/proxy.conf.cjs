const https = require('node:https');

module.exports = {
  '/api/**': {
    target: 'https://api:5081',
    changeOrigin: false,
    secure: true,
    // Public ACME certificates use the deployed domain, not the Compose service name.
    agent: new https.Agent({
      servername: process.env.DAHUB_TLS_SERVER_NAME || 'localhost',
    }),
  },
};
