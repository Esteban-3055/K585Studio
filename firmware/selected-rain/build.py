"""Build an analysis ELF and verify its exact placement. Never flashes a device."""
from pathlib import Path
import argparse,subprocess,struct,json,hashlib
HERE=Path(__file__).resolve().parent
EXPECTED={0x394c:132,0x39d8:32,0x3b12:94,0x3bf8:48,0x3f50:10,0x4c6c:188,0x523c:408,0x53e4:28}
HASHES={
 0x394c:'920806fdf8d64d7af1fcbc947cda8ef3b93def9824304547873cf589488a9b40',
 0x39d8:'f77c73e09f10a91eb9794250acea3d80a487cf05d3257f2c93fe03966a381c1d',
 0x3b12:'60dd38778baadd69da7387bf912676e40fd449b446b7b0ab55e8b581c16aedab',
 0x3bf8:'b1d0dc195299d5f98bbf4d22ba0b622f7ee31617e5588e6dbb3b6811c791601f',
 0x3f50:'6ba46c4a7b557c7f2db2003972c620fb25b16b53a2eec306bc0df1a78c8e2dab',
 0x4c6c:'a0cdb9c0e8a05addc2b0fffff3cf6a0c82f21e6c73272cc1d7376444096dfe71',
 0x523c:'db8935b48b4e474f99f3713b233bb1e39e578a00555c8dce430e254c6591bf41',
 0x53e4:'5c582d6a6d2a23202c8a0cc3797d10a0b2450ce3d3147e32459ecc6b2195f104'}
def sections(path):
 data=path.read_bytes();off=struct.unpack_from('<I',data,32)[0];size,count=struct.unpack_from('<HH',data,46);blocks={}
 for i in range(count):
  h=struct.unpack_from('<10I',data,off+i*size)
  if h[2]&2 and h[1]!=8 and h[5]:
   assert h[3]not in blocks;blocks[h[3]]=data[h[4]:h[4]+h[5]]
 return blocks
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--zig',required=True,type=Path);parser.add_argument('--output',required=True,type=Path);args=parser.parse_args()
 version=subprocess.run([str(args.zig),'version'],capture_output=True,text=True,check=True).stdout.strip()
 if version!='0.13.0':raise SystemExit('This measured candidate requires Zig 0.13.0; another compiler must be independently reverified.')
 args.output.mkdir(parents=True,exist_ok=True);elf=args.output/'selected-rain-analysis.elf'
 subprocess.run([str(args.zig),'cc','-target','thumb-freestanding-eabi','-mcpu=cortex_m0','-nostdlib','-nodefaultlibs','-Wl,-T,'+str(HERE/'selected.ld'),'-Wl,--gc-sections','-Wl,-e,rain_step',str(HERE/'selected.s'),str(HERE/'adapters.s'),'-o',str(elf)],check=True,capture_output=True)
 blocks=sections(elf)
 if {at:len(data)for at,data in blocks.items()}!=EXPECTED:raise SystemExit('Unexpected sections or sizes; do not use this candidate.')
 if {at:hashlib.sha256(data).hexdigest()for at,data in blocks.items()}!=HASHES:raise SystemExit('Candidate bytes changed; repeat independent emulation before updating the expected hashes.')
 report=dict(analysis_only=True,device_access=False,firmware_image_created=False,updater_created=False,compiler_version=version,
 total_bytes=sum(map(len,blocks.values())),excluding_native_dispatch_bytes=sum(len(data)for at,data in blocks.items()if at!=0x3f50),
 blocks=[dict(address=hex(at),bytes=len(data),sha256=hashlib.sha256(data).hexdigest())for at,data in sorted(blocks.items())])
 (args.output/'layout.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
