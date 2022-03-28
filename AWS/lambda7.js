const AWS = require('aws-sdk');
const s3 = new AWS.S3();
exports.handler = async (event) => {
    console.log('received request');
    const guid = event.pathParameters.guid;
    console.log('guid =', guid);
    const params = {
        Bucket: 'judgments-async',
        Key: guid
    };
    var head;
    try {
        head = await s3.headObject(params).promise();
    } catch (e) {
        if (e.statusCode === 404)
            return {
                statusCode: 404,
                headers: { 'Content-Type': 'text/plain' },
                body: "Not found",
                isBase64Encoded: false
            };
        return {
            statusCode: 500,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ status: 500, message: e.toString() }),
            isBase64Encoded: false
        };
    }
    const tnaStatus = head['Metadata']['tna-status'];
    console.log('TNA status =', tnaStatus);
    if (tnaStatus === '202')
        return {
            statusCode: 202,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token: guid }),
            isBase64Encoded: false
        };
    if (tnaStatus !== '200') {
        const json = (await (s3.getObject(params).promise())).Body.toString('utf-8');
        return JSON.parse(json);
    }
    const params2 = {
        Bucket: 'judgments-async',
        Key: guid,
        Expires: 300
    };
    const url = await s3.getSignedUrlPromise('getObject', params2);
    return {
        statusCode: 200,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: guid, url: url }),
        isBase64Encoded: false
    };
};
