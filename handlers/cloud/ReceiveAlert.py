import os
import json
import boto3

print('Loading function')

iot = boto3.client('iot-data')


def handler(event, context):
    # 単にペイロードをデバイスにバイパスする。
    topic = os.environ['MACKEREL_ALERT_TOPIC']
    print('Publish to: ' + topic)
    print(event)
    iot.publish(
        topic=topic,
        qos=0,
        payload=json.dumps(event, ensure_ascii=False)
    )
