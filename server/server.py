import data_file
import data_file_pb2 as pbdf
import data_comm_pb2 as pbdc
import data_comm_pb2_grpc as gdc
import grpc
from concurrent import futures
import sys
import math

test = pbdf.DataFile()


class TrajVis3DServicer(gdc.TrajVis3DServicer):
    dataFile = None

    def __init__(self, data_file: data_file.DataFile):
        self.data_file = data_file
        keys = list(data_file.instructions.keys())
        ret = pbdc.Properties()
        ret.center_lat, ret.center_lng = self.data_file.center
        ret.first_timestamp = min(keys)
        ret.last_timestamp = max(keys)
        print(math.cos(ret.center_lat*math.pi/180))
        ret.length_x = 111320 * math.cos(ret.center_lat*math.pi/180)*(data_file.coord_range[1][1]-data_file.coord_range[1][0])
        ret.length_z = 110574 * (data_file.coord_range[0][1]-data_file.coord_range[0][0])
        self.properties = ret
        print("properties:", self.properties, sep="\n")
        print("ready to serve. timestamp between {} and {}. "
              "total number of valid timestamps: {}. ".format(
            ret.first_timestamp,
            ret.last_timestamp,
            len(keys)
        ))

    def GetInstructionsBetween(self, request: pbdc.TimePeriod, context):
        print("request", request)
        insl = []
        for ts in range(request.start_ts, request.end_ts):
            if ts in self.data_file.instructions:
                _tmp = self.data_file.instructions[ts]
                print("{} serve {}".format(ts, len(_tmp)))
                for _tt in _tmp:
                    yield _tt
        print("serve instruction of length {}".format(len(insl)))

    def GetInstructionSetBetween(self, request, context):
        print("request", request)
        insl = []
        for ts in range(request.start_ts, request.end_ts):
            if ts in self.data_file.instructions:
                _tmp = self.data_file.instructions[ts]
                print("{} serve {}".format(ts, len(_tmp)))
                insl += _tmp
        print("create instruction set")
        ret = pbdc.InstructionSet()
        ret.instructions.extend(insl)
        print("serve instruction of length {}".format(len(insl)))
        return ret

    def GetProperties(self, request, context):
        return self.properties


cached = True


def serve():
    fuck = data_file.DataFile(test.SerializeToString())
    fuck.group_data()
    fuck.generate_flow()
    fuck.clean_intermediates()
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    gdc.add_TrajVis3DServicer_to_server(TrajVis3DServicer(fuck), server)
    server.add_insecure_port('127.0.0.1:9623')
    server.start()
    server.wait_for_termination()


if __name__ == "__main__":
    print("TrajVis3D Server Demo 4")
    if len(sys.argv) < 7:
        print("usage:\nserver.exe [file] [row#_uid] [row#_time] [row#_lat] [row#_lng] [coord_system]")
        print("currently supported coord_system: WGS84 and GCJ02")
    else:
        test.file = sys.argv[1]
        test.sep = ","
        test.has_header = False
        test.row_uid = int(sys.argv[2])
        test.row_time = int(sys.argv[3])
        test.coord_sys = pbdf.CoordinateSystem.WGS84 if sys.argv[6] in ["wgs84", "WGS84"] else pbdf.CoordinateSystem.GCJ02
        test.row_lat = int(sys.argv[4])
        test.row_lng = int(sys.argv[5])
        serve()
