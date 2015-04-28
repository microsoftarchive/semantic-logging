# Verifying data on Kibana
1. Once the cloud service is deployed, refresh your browser on the kibana dashboard. Check the "User event times to create index names" checkbox, it should use the logstash timestamp pattern to match and find some indices. Select Timestamp as the Time-field name and click Create      
![VerifyData-1.png](../md-images/VerifyData-1.png)
2. Click Discover at the top, and you should start seeing data  
![VerifyData-2.png](../md-images/VerifyData-2.png)
3. Your ELK stack is now up and running.  
