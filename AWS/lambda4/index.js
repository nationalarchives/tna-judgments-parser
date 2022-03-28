const AWS = require('aws-sdk');
const { v4: uuidv4 } = require('uuid');
const s3 = new AWS.S3({ region: 'eu-west-2' }); // , signatureVersion: 'v4'
exports.handler = async (event) => {
    const guid = uuidv4();
    const key = guid + '.request';
    const params = {
        Bucket: 'judgments-async',
        Key: key,
        ContentType: 'application/json',
        Expires: 300
    };
    const url = await s3.getSignedUrlPromise('putObject', params);
    return {
        statusCode: 200,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: guid, url: url }),
        isBase64Encoded: false
    };
};
