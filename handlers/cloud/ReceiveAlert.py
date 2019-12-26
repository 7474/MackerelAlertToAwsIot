import json
import os

# https://docs.aws.amazon.com/ja_jp/lambda/latest/dg/python-logging.html


def handler(event, context):
    print('## ENVIRONMENT VARIABLES')
    print(os.environ)
    print('## EVENT')
    print(event)
    return
