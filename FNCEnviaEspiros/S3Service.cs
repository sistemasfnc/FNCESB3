using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;


namespace FNCEnviaEspiros
{
    public class S3Service
    {
        private readonly IAmazonS3 _s3Client;

        public S3Service(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        public List<string> SearchFilesAsync(string bucketName, string searchPattern)
        {
            var files = new List<string>();
            try
            {
                ListObjectsV2Request request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    MaxKeys = 10 // Puedes ajustar este valor según tus necesidades
                };

                ListObjectsV2Response response;
                do
                {
                    response = _s3Client.ListObjectsV2(request);

                    foreach (S3Object entry in response.S3Objects)
                    {
                        if (entry.Key.Contains(searchPattern))
                        {
                            files.Add(entry.Key);
                        }
                    }

                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated.Value);

                return files;
            }
            catch (AmazonS3Exception e)
            {
                // Manejar la excepción
                throw;
            }
        }

        public Stream GetPdfStreamAsync(string bucketName, string keyName)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = keyName
                };
                var response = _s3Client.GetObject(request);
                var responseStream = new MemoryStream();
                response.ResponseStream.CopyTo(responseStream);
                responseStream.Position = 0;
                return responseStream;
            }
            catch (AmazonS3Exception e)
            {
                // Manejar la excepción
                throw;
            }
        }
    }
}
