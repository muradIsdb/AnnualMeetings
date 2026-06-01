import sys
content = open('src/IsDB.Hospitality.API/wwwroot/assets/index-placard-v1.js').read()
idx = content.find('function i4(')
chunk = content[idx:idx+40000]
start = int(sys.argv[1])
end = int(sys.argv[2])
print(repr(chunk[start:end]))
