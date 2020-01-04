import os
import greengrasssdk
import json
import sys
import logging

# Setup logging to stdout
logger = logging.getLogger(__name__)
logging.basicConfig(stream=sys.stdout, level=logging.DEBUG)

iot_client = greengrasssdk.client('iot-data')

thingName = os.environ['AWS_IOT_THING_NAME']

def get_read_topic(gpio_num):
    return '/'.join(['gpio', thingName, str(gpio_num), 'read'])

def get_write_topic(gpio_num):
    return '/'.join(['gpio', thingName, str(gpio_num), 'write'])

def send_message_to_connector(topic, message=''):
    iot_client.publish(topic=topic, payload=str(message))

def set_gpio_state(gpio, state):
    send_message_to_connector(get_write_topic(gpio), str(state))

def read_gpio_state(gpio):
    send_message_to_connector(get_read_topic(gpio))

def handler(event, context):
    logger.info("Received message!")
    logger.info(event)
    logger.info(type(event))

    # event
    # 1 : button off
    # 0 : button on

    state = 0
    if(event == 0):
        state = 1
    set_gpio_state(11, state)

    return