#!/bin/bash

sudo mkdir /datadisk
counter=`ls /datadisk/ | awk -Fk '{print $2}' | sort -n | tail -n1`
let counter=$counter+0
for hd in $( sudo fdisk -l 2>&1 | grep "contain" | awk '{print $2}' ); do
  echo -e "o\nn\np\n1\n\n\nw\n" | sudo fdisk $hd
  sudo mkfs -t ext4 ${hd}1
  sudo mkdir /datadisk/disk${counter}
  echo -e "${hd}1\t/datadisk/disk${counter}\text4\tdefaults\t0\t2" | sudo tee -a /etc/fstab
  sudo chown -R diag:diag /datadisk/disk${counter}
  let counter=$counter+1
done
sudo mount -a
sudo chown -R elasticSearch:elasticSearch /datadisk