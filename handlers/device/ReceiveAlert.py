import os
import greengrasssdk
import json
import sys
import logging
import time

# Setup logging to stdout
logger = logging.getLogger(__name__)
logging.basicConfig(stream=sys.stdout, level=logging.DEBUG)

iot_client = greengrasssdk.client('iot-data')

thingName = os.environ['AWS_IOT_THING_NAME']

def get_write_topic(gpio_num):
    return '/'.join(['gpio', thingName, str(gpio_num), 'write'])

def send_message_to_connector(topic, message=''):
    iot_client.publish(topic=topic, payload=str(message))

def set_gpio_state(gpio, state):
    send_message_to_connector(get_write_topic(gpio), str(state))

def handler(event, context):
    logger.info("Received message!")
    logger.info(event)
    logger.info(type(event))

    status_to_sec = {'ok': 3, 'warning': 6, 'critical': 9, 'unknown': 9}
    taiyo_sec = status_to_sec.get(event.detail.alert.status, 9)

    # 太陽拳する
    set_gpio_state(11, 1)
    time.sleep(taiyo_sec)
    set_gpio_state(11, 0)

    return