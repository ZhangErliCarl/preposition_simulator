from distutils.util import execute
import requests
from PyQt5 import QtCore, QtWidgets, QtGui
import time
import json
import sys
import subprocess
import os


class UnityCom:
    def __init__(self, url='127.0.0.1', port='8080', x_display=None, no_graphics=False,
                 timeout_wait=50):
        self._address = 'http://' + url + ':' + port
        self.port = port
        self.graphics = no_graphics
        self.x_display = x_display
        self.timeout_wait = timeout_wait

    def post_command(self, request_dict, repeat=False):
        try:
            if repeat:
                resp = self.requests_retry_session().post(self._address, json=request_dict)
            else:
                resp = requests.post(
                    self._address, json=request_dict, timeout=self.timeout_wait)      
            if resp.status_code != requests.codes.ok:
                print(resp.json())
                # raise UnityEngineException(resp.status_code, resp.json())
            return resp.json()
        except requests.exceptions.RequestException as e:
            print(str(e))
            return
            # raise UnityCommunicationException(str(e))

    def switch_camera(self, cameras=[0]):
        request = {'id': str(time.time()),
                   'action': 'switch_camera', 'intParams': cameras}
        print(request)
        response = self.post_command(request)
        return response['success'] if response else None

    def randomize_scene(self):
        request = {'id': str(time.time()), 'action': 'randomize_scene'}
        print(request)
        response = self.post_command(request)
        return response['success'] if response else None

    def add_character(self, character_resource='Chars/Male1', position=None, initial_room=""):
        """
        Add a character in the scene.

        :param str character_resource: which game object to use for the character
        :param int char_index: the index of the character you want to move
        :param list position: the position where you want to place the character
        :param str initial_room: the room where you want to put the character,
        if positon is not specified. If this is not specified, it places character in random location
        :return: succes (bool)
        """
        mode = 'random'
        pos = [0, 0, 0]
        if position is not None:
            mode = 'fix_position'
            pos = position
        elif not len(initial_room) == 0:
            assert initial_room in ["kitchen",
                                    "bedroom", "livingroom", "bathroom"]
            mode = 'fix_room'

        request = {'id': str(time.time()), 'action': 'add_character',
                   'stringParams': [json.dumps({
                       'character_resource': character_resource,
                       'mode': mode,
                       'character_position': {'x': pos[0], 'y': pos[1], 'z': pos[2]},
                       'initial_room': initial_room
                   })]}
        print(request)
        response = self.post_command(request)
        return response['success'] if response else None

    def render_script(self, script, randomize_execution=False, random_seed=-1, processing_time_limit=10,
                      skip_execution=False, find_solution=False, output_folder='Output/', file_name_prefix="script",
                      frame_rate=5, image_synthesis=['normal'], save_pose_data=False,
                      image_width=640, image_height=480, recording=False, record=False,
                      save_scene_states=False, camera_mode=['AUTO'], time_scale=1.0, skip_animation=False):
        """
        Executes a script in the simulator. The script can be single or multi agent,
        and can be used to generate a video, or just to change the state of the environment

        :param list script: a list of script lines, of the form `['<char{id}> [{Action}] <{object_name}> ({object_id})']`
        :param bool randomize_execution: randomly choose elements
        :param int random_seed: random seed to use when randomizing execution, -1 means that the seed is not set
        :param bool find_solution: find solution (True) or use graph ids to determine object instances (False)
        :param int processing_time_limit: time limit for finding a solution in seconds
        :param int skip_execution: skip rendering, only check if a solution exists
        :param str output_folder: folder to output renderings
        :param str file_name_prefix: prefix of created files
        :param int frame_rate: frame rate at which to generate the video
        :param str image_synthesis: what information to save. Can be multiple at the same time. Modes are: "normal", "seg_inst", "seg_class", "depth", "flow", "albedo", "illumination", "surf_normals". Leave empty if you don't want to generate anythign
        :param bool save_pose_data: save pose data, a skeleton for every agent and frame
        :param int image_width: image_height for the generated frames
        :param int image_height: image_height for the generated frames
        :param bool recoring: whether to record data with cameras
        :param bool save_scene_states: save scene states (this will be unused soon)
        :param list camera_mode: list with cameras used to render data. Can be a str(i) with i being a scene camera index or one of the cameras from `character_cameras`
        :param int time_scale: accelerate time at which actions happen
        :param bool skip_animation: whether agent should teleport/do actions without animation (True), or perform the animations (False)

        :return: pair success (bool), message: (str)
        """
        params = {'randomize_execution': randomize_execution, 'random_seed': random_seed,
                  'processing_time_limit': processing_time_limit, 'skip_execution': skip_execution,
                  'output_folder': output_folder, 'file_name_prefix': file_name_prefix,
                  'frame_rate': frame_rate, 'image_synthesis': image_synthesis,
                  'find_solution': find_solution,
                  'save_pose_data': save_pose_data, 'save_scene_states': save_scene_states,
                  'camera_mode': camera_mode, 'recording': recording, 'record': record,
                  'image_width': image_width, 'image_height': image_height,
                  'time_scale': time_scale, 'skip_animation': skip_animation}
        request = {'id': str(time.time()), 'action': 'render_script',
                   'stringParams': [json.dumps(params)] + script}
        print(request)
        response = self.post_command({'id': str(time.time()), 'action': 'render_script',
                                      'stringParams': [json.dumps(params)] + script})

        try:
            message = json.loads(response['message'])
        except ValueError:
            message = response['message']

        return response['success'], message if response else None, None

    def reset(self, scene_index=None):
        """
        Reset scene. Deletes characters and scene chnages, and loads the scene in scene_index


        :param int scene_index: integer between 0 and 6, corresponding to the apartment we want to load
        :return: succes (bool)
        """
        print(scene_index)
        response = self.post_command({'id': str(time.time()), 'action': 'reset',
                                      'intParams': [] if scene_index is None else [scene_index]})
        return response['success'] if response else None


class UnityEngineException(Exception):
    """
    This exception is raised when an error in communication occurs:
    - Unity has received invalid request
    More information is in the message.
    """

    def __init__(self, status_code, resp_dict):
        resp_msg = resp_dict['message'] if 'message' in resp_dict else 'Message not available'
        self.message = 'Unity returned response with status: {0} ({1}), message: {2}'.format(
            status_code, requests.status_codes._codes[status_code][0], resp_msg)


class UnityCommunicationException(Exception):
    def __init__(self, message):
        self.message = message


class MyWidget(QtWidgets.QMainWindow):
    teacher_index = -1
    baby_index = -1
    current_index = 0
    exe_name = ""
    camera_list = []

    def __init__(self):
        super().__init__()
        self.comm = UnityCom()

        self.startButton = QtWidgets.QPushButton("Start Simulator")
        self.resetButton = QtWidgets.QPushButton("Reset")
        self.randomButton = QtWidgets.QPushButton("Randomize_scene")
        self.addTeacherButton = QtWidgets.QPushButton("Add Teacher")
        self.addBabyButton = QtWidgets.QPushButton("Add Baby")
        self.confirmButton = QtWidgets.QPushButton("Confirm")
        self.b6 = QtWidgets.QCheckBox("Record")

        self.b1 = QtWidgets.QCheckBox("Camera 1")
        self.b2 = QtWidgets.QCheckBox("Camera 2")
        self.b3 = QtWidgets.QCheckBox("Camera 3")
        self.b4 = QtWidgets.QCheckBox("Camera 4")
        self.b5 = QtWidgets.QCheckBox("Baby Camera")

        self.b1.stateChanged.connect(lambda: self.btnstate(self.b1))
        self.b2.stateChanged.connect(lambda: self.btnstate(self.b2))
        self.b3.stateChanged.connect(lambda: self.btnstate(self.b3))
        self.b4.stateChanged.connect(lambda: self.btnstate(self.b4))
        self.b5.stateChanged.connect(lambda: self.btnstate(self.b5))

        self.characterbox = QtWidgets.QComboBox()
        self.characterbox.addItems(
            ["Teacher", "Baby"])
        self.combobox = QtWidgets.QComboBox()

        self.combobox.addItems(
            ["ball", "teddybear", "toy", "numberbox", "cube", "train", "car", "horseswing", "standinglamp", "piano", "bangku", "crib"])
        self.actionbox = QtWidgets.QComboBox()
        self.actionbox.addItems(
            ["walk", "run", "crawl", "lookat", "touch", "grab", "rotate", "putback", "check", "walkforward", "walkbackward", "turnleft", "turnright", "lookaround", "wander"])

        self.resetButton.clicked.connect(self.reset)
        self.randomButton.clicked.connect(self.random)
        self.addTeacherButton.clicked.connect(self.addTeacher)
        self.addBabyButton.clicked.connect(self.addBaby)
        self.confirmButton.clicked.connect(self.run)
        self.startButton.clicked.connect(self.start_simulator)

        self.setWindowTitle('MyWindow')
        self._main = QtWidgets.QWidget()
        self.setCentralWidget(self._main)

        layout = QtWidgets.QGridLayout(self._main)
        layout.addWidget(self.startButton)
        layout.addWidget(self.resetButton)
        layout.addWidget(self.randomButton)
        layout.addWidget(self.addTeacherButton)
        layout.addWidget(self.addBabyButton)

        layout.addWidget(self.b1)
        layout.addWidget(self.b2)
        layout.addWidget(self.b3)
        layout.addWidget(self.b4)
        layout.addWidget(self.b5)

        self.b5.hide()

        layout.addWidget(self.characterbox)
        layout.addWidget(self.actionbox)
        layout.addWidget(self.combobox)
        layout.addWidget(self.confirmButton)
        layout.addWidget(self.b6)

    def btnstate(self, b):
        numberOfCheckboxesChecked = 4
        print(b.text())
        index = 0
        if (b.text() == "Baby Camera"):
            index = 4
        else:
            index = (int)(b.text().split(" ")[1])
            index -= 1
        if len(self.camera_list) >= numberOfCheckboxesChecked and b.isChecked():
            b.setChecked(False)
            return
        else:
            if b.isChecked():
                self.camera_list.append(index)
            else:
                if index in self.camera_list:
                    self.camera_list.remove(index)

        print(self.camera_list)
        self.comm.switch_camera(cameras=self.camera_list)

    def addTeacher(self):
        self.teacher_index = self.current_index
        self.current_index += 1
        self.comm.add_character('Chars/Teacher')

    def addBaby(self):
        self.baby_index = self.current_index
        self.current_index += 1
        self.comm.add_character('Chars/Baby')
        self.b5.show()

    def random(self):
        self.comm.randomize_scene()

    def reset(self):
        self.current_index = 0
        self.comm.reset()

    def run(self):
        print(self.characterbox.currentIndex())
        if (self.characterbox.currentIndex() == 0):
            index = self.teacher_index
        else:
            index = self.baby_index
        action = str(self.actionbox.currentText())
        if (self.actionbox.currentIndex() < 8):
            item = str(self.combobox.currentText())
            self.execute(index, action, item=item, record=self.b6.isChecked())

        else:
            self.execute(index, action, record=self.b6.isChecked())

    def execute(self, index, action, item="", record=False):
        if item:
            script = ['<char{}> [{}] <{}> (1)']
            script[0] = script[0].format(index, action, item)
        else:
            script = ['<char{}> [{}]']
            script[0] = script[0].format(index, action)
        self.comm.render_script(
            script, find_solution=True, record=record)

    def start_simulator(self):
        options = QtWidgets.QFileDialog.Options()
        options |= QtWidgets.QFileDialog.DontUseNativeDialog
        self.exe_name, _ = QtWidgets.QFileDialog.getOpenFileName(
            self, "QFileDialog.getOpenFileName()", "", "All Files (*);;Executable Files (*.exe)", options=options)
        if self.exe_name:
            print(self.exe_name)
            executable = os.path.basename(self.exe_name)
            print(self.process_exists(executable))
            if not self.process_exists(executable):
                subprocess.Popen([self.exe_name, "-c"], close_fds=True)

    def process_exists(self, process_name):
        call = 'TASKLIST', '/FI', 'imagename eq %s' % process_name
        # use buildin check_output right away
        output = subprocess.check_output(call).decode()
        # check in last line for process name
        last_line = output.strip().split('\r\n')[-1]
        # because Fail message could be translated
        return last_line.lower().startswith(process_name.lower())


if __name__ == "__main__":
    app = QtWidgets.QApplication([])

    widget = MyWidget()
    widget.resize(600, 400)
    widget.show()

    sys.exit(app.exec())
