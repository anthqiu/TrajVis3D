import protobuf.data_file_pb2 as dfpb
import data_file as df

test = dfpb.DataFile()

test.file = "../../data/gps_20161101_morning"
test.sep = ","
test.has_header = True
test.row_uid = 0
test.row_time = 1
test.coord_sys = dfpb.CoordinateSystem.GCJ02
test.row_lat = 3
test.row_lng = 2

print(test.SerializeToString())
print(test)
