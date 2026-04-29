using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace FNCUtils
{    
    public class AWSConnector
    {
        public IAmazonS3 s3Client { get; set; }

        private string accessKey { get; set; }
        private string secretKey { get; set; }

        private readonly RegionEndpoint bucketRegion = RegionEndpoint.USEast1;

        public AWSConnector(string accesskey, string secretkey) 
        { 
            this.accessKey = accesskey;
            this.secretKey = secretkey;
        }

        public void Connect()
        {
            this.s3Client = new AmazonS3Client(this.accessKey, this.secretKey, this.bucketRegion);
        }

        public byte[] DownloadFileAsync(string filePath, string bucketName)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = filePath
                };
                using (GetObjectResponse response = s3Client.GetObject(request))
                using (MemoryStream ms = new MemoryStream())
                {
                    response.ResponseStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public List<string> ListKeys(string bucketName, string prefix)
        {
            var keys = new List<string>();
            string token = null;

            do
            {
                var req = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix,
                    ContinuationToken = token
                };

                var resp = s3Client.ListObjectsV2(req);
                keys.AddRange(resp.S3Objects.Select(o => o.Key));
                if (resp.IsTruncated.HasValue && resp.IsTruncated.Value)
                {
                    token = resp.NextContinuationToken;
                }
                else 
                    token = null;
            } 
            while (token != null);
            return keys;
        }
    }
}
