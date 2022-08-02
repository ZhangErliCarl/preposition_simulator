import sys
import random
from PyQt5 import QtCore, QtWidgets, QtGui
import requests
import time
import json


class Worker(QtCore.QObject):

    finished = QtCore.pyqtSignal()  # give worker class a finished signal

    def __init__(self, parent=None):
        QtCore.QObject.__init__(self, parent=parent)
        self.continue_run = True  # provide a bool run condition for the class

    def do_work(self):
        print("do")
        self.continue_run = True
        i = 1
        while self.continue_run:  # give the loop a stoppable condition
            print(i)
            QtCore.QThread.sleep(1)
            i = i + 1
        self.finished.emit()  # emit the finished signal when the loop is done

    def stop(self):
        self.continue_run = False  # set the run condition to false on stop


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
                # print(resp.json())
                raise UnityEngineException(resp.status_code, resp.json())
            return resp.json()
        except requests.exceptions.RequestException as e:
            print(str(e))
            raise UnityCommunicationException(str(e))

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
        response = self.post_command(
            {'id': str(time.time()), 'action': 'add_character',
             'stringParams': [json.dumps({
                 'character_resource': character_resource,
                 'mode': mode,
                 'character_position': {'x': pos[0], 'y': pos[1], 'z': pos[2]},
                 'initial_room': initial_room
             })]})
        return response['success']

    def render_script(self, script, randomize_execution=False, random_seed=-1, processing_time_limit=10,
                      skip_execution=False, find_solution=False, output_folder='Output/', file_name_prefix="script",
                      frame_rate=5, image_synthesis=['normal'], save_pose_data=False,
                      image_width=640, image_height=480, recording=False,
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
                  'camera_mode': camera_mode, 'recording': recording,
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

        return response['success'], message

    def reset(self, scene_index=None):
        """
        Reset scene. Deletes characters and scene chnages, and loads the scene in scene_index


        :param int scene_index: integer between 0 and 6, corresponding to the apartment we want to load
        :return: succes (bool)
        """
        print(scene_index)
        response = self.post_command({'id': str(time.time()), 'action': 'reset',
                                      'intParams': [] if scene_index is None else [scene_index]})
        return response['success']


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


class MyThread(QtCore.QThread):
    signal = QtCore.pyqtSignal(int)

    def __init__(self, continuous=False, parent=None):
        super(MyThread, self).__init__(parent)
        self._stopped = True
        self.continuous = continuous
        self.i = 0

    def __del__(self):
        self.wait()

    def stop(self):
        self._stopped = True

    def run(self):
        self._stopped = False

        while not self._stopped:
            print(self._stopped)
            self.signal.emit(self.i)
            if self.continuous:
                QtCore.QThread.sleep(2)
            else:
                break


class MyWidget(QtWidgets.QMainWindow):
    stop_signal = QtCore.pyqtSignal()

    def __init__(self):
        super().__init__()
        self.comm = UnityCom()
        # self.hello = ["Hello World", "Hi"]

        # self.activeButton = QtWidgets.QPushButton("Active Learning")
        self.addTeacherButton = QtWidgets.QPushButton("Teaching Mode")
        self.confirmButton = QtWidgets.QPushButton("Confirm")
        # self.text = QtWidgets.QLabel("Hello World",
        #                              alignment=QtCore.Qt.AlignCenter)
        self.combobox = QtWidgets.QComboBox()
        self.combobox.addItems(
            ["teddybear", "toy", "number box", "truck", "ball", "train", "crib"])
        self.actionbox = QtWidgets.QComboBox()
        self.actionbox.addItems(["touch", "grab", "rotate", "putback"])

        # self.layout = QtWidgets.QVBoxLayout(self)
        # self.layout.addWidget(self.text)
        # self.layout.addWidget(self.activeButton)
        # self.layout.addWidget(self.addTeacherButton)
        # self.layout.addWidget(QtWidgets.QLabel("Teach "))

        # self.layout.addWidget(self.confirmButton)

        # # self.activeButton.clicked.connect(self.active)
        # # self.addTeacherButton.clicked.connect(self.addTeacher)
        self.confirmButton.clicked.connect(self.teach)

        self.setWindowTitle('MyWindow')
        self._main = QtWidgets.QWidget()
        self.setCentralWidget(self._main)
        self.button = QtWidgets.QPushButton('Active Learning')
        self.button.clicked.connect(self.do)

        self.contincheck = QtWidgets.QCheckBox("Continuous")
        self.contincheck.clicked.connect(self.continuous_doing)
        self.continuous = True
        layout = QtWidgets.QGridLayout(self._main)
        layout.addWidget(self.button, 0, 0)
        layout.addWidget(self.addTeacherButton)
        layout.addWidget(self.actionbox)
        layout.addWidget(self.combobox)
        layout.addWidget(self.confirmButton)
        # layout.addWidget(self.contincheck)

        self.mythread = MyThread(self.continuous, self)
        self.mythread.finished.connect(self.thread_finished)
        self.addTeacherButton.clicked.connect(self.teaching)
        self.mythread.signal.connect(self.done)

    def teaching(self):
        self.mythread.stop()
        self.comm.reset()
        self.comm.add_character('Chars/Teacher')
        self.comm.add_character('Chars/Baby')

    def continuous_doing(self):
        self.button.setCheckable(self.contincheck.isChecked())
        self.continuous = self.contincheck.isChecked()

    def do(self):
        try:
            self.comm.reset()
            self.comm.add_character('Chars/Baby')
            self.mythread.start()
        except Exception as e:
            print(e)

    @QtCore.pyqtSlot(int)
    def done(self, i):
        try:
            print(i)
            script = ['<char0> [wander] (1)']
            self.comm.render_script(script, find_solution=True)
        except Exception as e:
            self.mythread.stop

    @QtCore.pyqtSlot()
    def thread_finished(self):
        print('thread finished')

    def start_thread(self):
        self.thread.start
        # self.thread.run

    def stop_thread(self):
        self.stop_signal.emit()
        self.mythread.stop

    # @QtCore.Slot()
    # def magic(self):
    #     self.text.setText(random.choice(self.hello))

    def active(self):
        self.comm.reset()
        self.comm.add_character('Chars/Baby')
        active = True
        script = ['<char0> [wander] (1)']
        while (active):
            self.comm.render_script(script, find_solution=True)

    def addTeacher(self):
        self.comm.add_character('Chars/Teacher')

    def teach(self):
        dest = str(self.combobox.currentText())
        action = str(self.actionbox.currentText())
        # script = ['<char1> [walk] <{}> (1)', '<char1> [touch] <{}> (1)']
        script = ['<char0> [{}] <{}> (1)']
        script[0] = script[0].format(action, dest)
        self.comm.render_script(script, find_solution=True)


if __name__ == "__main__":
    app = QtWidgets.QApplication([])

    widget = MyWidget()
    widget.resize(600, 400)
    widget.show()

    sys.exit(app.exec())
