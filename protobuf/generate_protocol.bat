protoc.exe -I=. --python_out=..\server .\data_file.proto
protoc.exe -I=. --python_out=..\server .\data_comm.proto

protoc.exe -I=. --csharp_out=..\client\Assets\Scripts\Protobuf .\data_file.proto
protoc.exe -I=. --csharp_out=..\client\Assets\Scripts\Protobuf .\data_comm.proto

protoc.exe -I=. --grpc_out=..\server .\data_comm.proto --plugin=protoc-gen-grpc=grpc_python_plugin.exe
protoc.exe -I=. --grpc_out=..\client\Assets\Scripts\Protobuf .\data_comm.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe

pause