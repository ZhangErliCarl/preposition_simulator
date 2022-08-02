import requests
import time
import json
import sys
import subprocess
import os
address = 'http://127.0.0.1:8080'
request_dict = {'id': str(time.time()), 'action': 'randomize_scene'}
resp = requests.post(address, json=request_dict, timeout=50)      
print(resp.json())