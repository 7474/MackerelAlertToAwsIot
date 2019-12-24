import RPi.GPIO as GPIO
import random

# https://docs.aws.amazon.com/ja_jp/greengrass/latest/developerguide/raspberrypi-gpio-connector.html
# https://sourceforge.net/p/raspberry-gpio-python/wiki/Home/

stat_ok = 9
stat_warning = 10
stat_critical = 11
chan_list = [stat_ok, stat_warning, stat_critical]

# TODO GPIOの状態は前回実行時の状態を引き継いでほしいが、リセットされないか？
GPIO.setmode(GPIO.BCM)
GPIO.setup(chan_list, GPIO.OUT)


def hl():
    # TODO 後で消す
    return 1 if random.random() > 0.5 else 0


def handler(event, context):
    # TODO 状態に応じてピン状態を変更
    GPIO.output(stat_ok, hl())
    GPIO.output(stat_warning, hl())
    GPIO.output(stat_critical, hl())
    return
