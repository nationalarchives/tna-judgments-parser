const AWS = require('aws-sdk');
const s3 = new AWS.S3();
exports.handler = async (event) => {
    const guid = event.pathParameters.guid
    const params = {
        Bucket: 'judgments-async',
        Key: guid
    };
    try {
        const json = (await (s3.getObject(params).promise())).Body.toString('utf-8');
        return JSON.parse(json);
    } catch (e) {
        return {
            statusCode: 404,
            headers: {
                'Content-Type': "text/plain"
            },
            isBase64Encoded: false,
            body: "not found"
        };
    }
};
