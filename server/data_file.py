import data_file_pb2 as dfpb
import data_comm_pb2 as dcpb
import pandas as pd
import math
import time
import random


def gcj_to_wgs(lon, lat):
    a = 6378245.0  # 克拉索夫斯基椭球参数长半轴a
    ee = 0.00669342162296594323  # 克拉索夫斯基椭球参数第一偏心率平方
    PI = 3.14159265358979324  # 圆周率
    # 以下为转换公式
    x = lon - 105.0
    y = lat - 35.0
    # 经度
    dLon = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * math.sqrt(abs(x))
    dLon += (20.0 * math.sin(6.0 * x * PI) + 20.0 * math.sin(2.0 * x * PI)) * 2.0 / 3.0
    dLon += (20.0 * math.sin(x * PI) + 40.0 * math.sin(x / 3.0 * PI)) * 2.0 / 3.0
    dLon += (150.0 * math.sin(x / 12.0 * PI) + 300.0 * math.sin(x / 30.0 * PI)) * 2.0 / 3.0
    # 纬度
    dLat = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * math.sqrt(abs(x))
    dLat += (20.0 * math.sin(6.0 * x * PI) + 20.0 * math.sin(2.0 * x * PI)) * 2.0 / 3.0
    dLat += (20.0 * math.sin(y * PI) + 40.0 * math.sin(y / 3.0 * PI)) * 2.0 / 3.0
    dLat += (160.0 * math.sin(y / 12.0 * PI) + 320 * math.sin(y * PI / 30.0)) * 2.0 / 3.0
    radLat = lat / 180.0 * PI
    magic = math.sin(radLat)
    magic = 1 - ee * magic * magic
    sqrtMagic = math.sqrt(magic)
    dLat = (dLat * 180.0) / ((a * (1 - ee)) / (magic * sqrtMagic) * PI)
    dLon = (dLon * 180.0) / (a / sqrtMagic * math.cos(radLat) * PI)
    wgsLon = lon - dLon
    wgsLat = lat - dLat
    return wgsLon, wgsLat


def operate_data(gid, uid, data):
    if gid % 1000 == 0:
        print("processing gid {:6d} uid {}".format(gid, uid))
    data = data.sort_values(by="time") \
        .loc[(data["lat"] != data["lat"].shift(-1)) |
             (data["lng"] != data["lng"].shift(-1))] \
        .reset_index(drop=True)
    return uid, data


class DataFile:
    def __init__(self, message):
        self.proto = dfpb.DataFile()
        self.proto.ParseFromString(message)
        self.data = None
        self.grouped_data = None
        self.groups = None
        self.instructions = {}
        self.center = (0, 0)
        self.coord_range = []
        self.read_file()
        if self.proto.coord_sys == dfpb.CoordinateSystem.GCJ02:
            self.gcj_to_wgs()

    def clean_intermediates(self):
        self.data = None
        self.grouped_data = None
        self.groups = None

    def update(self, message):
        new_proto = dfpb.DataFile()
        new_proto.ParseFromString(message)
        self.proto = new_proto
        if not new_proto.file == self.proto.file:
            self.read_file()

    def read_file(self):
        print("read file {}".format(self.proto.file))
        ts = time.time()
        header = [0] if self.proto.has_header else []
        self.data = pd.read_csv(
            self.proto.file,
            sep=self.proto.sep,
            skiprows=header,
            error_bad_lines=False,
            header=None
        )
        print(self.data.tail())
        print("[{:.2f}] rename columns".format(time.time() - ts))
        self.data = self.data.rename(columns={
            self.proto.row_uid: "uid",
            self.proto.row_time: "time",
            self.proto.row_lat: "lat",
            self.proto.row_lng: "lng"
        })[["uid", "time", "lat", "lng"]]
        print("[{:.2f}] drop na".format(time.time() - ts))
        self.data = self.data.dropna().reset_index(drop=True)
        if self.data["time"].dtype == "object":
            self.data["time"] = self.data["time"].astype("datetime64[s]").values.astype("uint64")/1e9
        self.data["time"] = self.data["time"].astype("int")
        self.data["lat"] = self.data["lat"].astype("float")
        self.data["lng"] = self.data["lng"].astype("float")
        self.coord_range.append((self.data["lat"].min(), self.data["lat"].max()))
        self.coord_range.append((self.data["lng"].min(), self.data["lng"].max()))
        self.center = (sum(i)/2 for i in self.coord_range)
        print("[{:.2f}] read file complete".format(time.time() - ts))

    def group_data(self):
        print("group data")
        ts = time.time()
        self.grouped_data = []
        self.groups = []
        gid = 0
        tmp = self.data.groupby("uid")
        print(len(tmp))
        for uid, data in tmp:
            u, d = operate_data(gid, uid, data)
            gid += 1
            if len(d) > 0:
                self.groups.append(u)
                self.grouped_data.append(d)
            else:
                print("group {:6d} is empty!".format(u))
        print("group data end. time spent: {:.2f}".format(time.time() - ts))

    def generate_flow(self):
        instructions = []
        print("generate flow")
        start_ts = time.time()
        # frac = random.sample(self.grouped_data, 1000)
        # print(len(frac))
        # for i, d in enumerate(frac):
        print(len(self.grouped_data))
        for i, d in enumerate(self.grouped_data):
            if i % 1000 == 0:
                print("[{:.2f}] process group {}".format(time.time() - start_ts, i))
            d["lat_end"] = d["lat"].shift(-1)
            d["time_end"] = d["time"].shift(-1)
            d["lng_end"] = d["lng"].shift(-1)
            d.dropna(inplace=True)
            d["time_end"] = d["time_end"].astype("int")
            if d.shape[0] == 0: continue
            d["end"] = 0
            d.iloc[-1, 7] = 1
            instructions.append(d)
        print("[{:.2f}] concat instructions".format(time.time() - start_ts))
        instruction_groups = pd.concat(instructions).reset_index(drop=True).groupby("time")
        print("[{:.2f}] start generate instruction".format(time.time() - start_ts))
        self.instructions = {}
        _tmp_i = []

        gid = 0
        print(len(instruction_groups))

        for ts, tdata in instruction_groups:
            gid += 1
            self.instructions[ts] = []
            _tmp = []
            for _, trow in tdata.iterrows():
                inst = dcpb.Instruction()
                inst.uid = trow["uid"]
                inst.start_ts = trow["time"]
                inst.start_lat = trow["lat"]
                inst.start_lng = trow["lng"]
                inst.is_end_instruction = trow["end"]
                inst.end_ts = trow["time_end"]
                inst.end_lat = trow["lat_end"]
                inst.end_lng = trow["lng_end"]
                _tmp.append(inst)
            _tmp_i.append((ts, _tmp))
            if gid % 1000 == 0:
                print("[{:.2f}] gid #{}: timestamp {}".format(time.time() - start_ts, gid, ts))
        self.instructions = {k: v for k, v in _tmp_i}
        print("[{:.2f}] generate flow completed".format(time.time() - start_ts))

    def gcj_to_wgs(self):
        print("converting coordinate")
        ts = time.time()
        self.data["lng"], self.data["lat"] = zip(*self.data.apply(lambda x: gcj_to_wgs(x["lng"], x["lat"]), axis=1))
        print("[{:.2f}] done".format(time.time() - ts))

    def store_data_as_file(self):
        print("saving file to {}".format(self.proto.file + "_converted"))
        self.data.to_csv(self.proto.file + "_converted", header=False, index_label=False)
