﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Models;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DBLayer.Repo.Implementation;
using Models.Data;
using DBLayer.Repo.Interfaces;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Drawing;

namespace Business_Api.Controllers
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController:ControllerBase
    {
        private string _configurationKey;
        private string _configurationVector;
        private IEmployeeRepo _employeeRepo;
        
        public UserController(IEmployeeRepo employeeRepo, IConfiguration configurations)
        {
            _configurationKey = configurations.GetSection("MySettings").GetSection("Key").Value;
            _configurationVector = configurations.GetSection("MySettings").GetSection("Vector").Value;
            _employeeRepo = employeeRepo;
        }

        /// <summary>
        /// Size of hash.
        /// </summary>
        private const int HashSize = 20;
        [HttpGet("GetEmployeesInfo")]
        public async  Task<ActionResult<IEnumerable<Employee>>> GetEmployees() 
        {
           var employeeDetails= await _employeeRepo.GetAll();
            return employeeDetails.ToList();
        }

        [HttpPost("GetEmployeeInfo")]
        public async Task<ActionResult<Users>> GetEmployee(string email)
        {
            var employeeDetail = await _employeeRepo.GetEmployee(email);

            Users user = new Users();

            user.Email = employeeDetail.Email;
            user.EmployeeName = employeeDetail.Name;
            user.PAN = DecryptPassword(employeeDetail.PAN);
            user.Password = DecryptPassword(employeeDetail.Password);
            user.ProfilePicture = employeeDetail.ProfilePicturePath;
            user.LastUpdateComment = employeeDetail.LastUpdateComment;
            user.ProfileLink = employeeDetail.ProfileLink;
            user.IsAdmin = employeeDetail.IsAdmin;
            user.Role = employeeDetail.Role;

            return user;
        }

        [HttpPost("DeleteEmployeeInfo")]
        public async Task<ActionResult<int>> DeleteEmployee(string email)
        {
            Employee employee = new Employee();
            employee.Email = email;
            return await _employeeRepo.Delete(employee);
        }

        [HttpPost("LoginEmployee")]
        public async Task<ActionResult<HttpStatusCode>> LoginEmployee(Users user)
        {
            var employeeDetail = await _employeeRepo.GetEmployee(user.Email);
            if (employeeDetail!= null && employeeDetail.IsActive)
            {
                var password = DecryptPassword(employeeDetail.Password);

                if (password == user.Password)
                {
                    return HttpStatusCode.OK;
                }
            }

            return HttpStatusCode.NotFound;
        }

        [HttpPost("AddEmployees")]
        public async Task<ActionResult<int>> SaveUsers(Users user)
        {
            try
            {
                Employee employee = new Employee();
                employee.Name = user.EmployeeName;
                employee.Password = EncryptedPassword(user.Password);
                employee.Email = user.Email;
                employee.CreationTimeStamp = DateTime.UtcNow;
                employee.IsActive = true;
                employee.IsLocked = false;
                employee.IsAdmin = user.IsAdmin;
                if (user.IsAdmin) {
                    employee.Role = "18";
                }
                else
                    employee.Role = "15";

                employee.PAN = EncryptedPassword(user.PAN); 
                employee.ProfilePicturePath = user.ProfilePicture;
                employee.LastUpdateComment = user.LastUpdateComment;
                employee.ProfileLink = user.ProfileLink;
                var hasAdded = await _employeeRepo.Add(employee);

                if (hasAdded > 0)
                {
                    return hasAdded;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            return 0;
        }

        [HttpPost("UpdateEmployees")]
        public async Task<ActionResult<int>> UpdateEmployee(Users user)
        {
            try
            {
                var employeeDetail = await _employeeRepo.GetEmployee(user.Email);
                Employee employee = new Employee();
                employeeDetail.Name = user.EmployeeName;
                employeeDetail.Password = EncryptedPassword(user.Password);
                employeeDetail.Email = user.Email;
                employeeDetail.UpdateTimeStamp = DateTime.UtcNow;
                employeeDetail.PAN = EncryptedPassword(user.PAN);
                if (IsValidImage(user.ProfilePicture))
                {
                    employeeDetail.ProfilePicturePath = user.ProfilePicture;
                }
                else
                {
                    throw new ArgumentException("Profile Picture submitted is not valid.");
                }
                
                employeeDetail.ProfileLink = user.ProfileLink;
                employeeDetail.LastUpdateComment = user.LastUpdateComment;            
                var hasUpdated = await _employeeRepo.Update(employeeDetail);

                if (hasUpdated > 0)
                {
                    return hasUpdated;
                }
            }
            catch(Exception ex) 
            {
                throw;
            }
            return 0;
        }
        private bool IsValidImage(string img)
        {
            try
            {                
                byte[] bytes = Convert.FromBase64String(img.Split(',')[1]);

                System.Drawing.Image image;
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    image = Image.FromStream(ms);
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        //yet to work on this
        [HttpPost("DeleteEmployee")]
        public async Task<ActionResult<IEnumerable<Users>>> DeleteEmployee(Users user)
        {
            Employee employee = new Employee();
            employee.Name = user.EmployeeName;
            employee.Email = user.Email;
            employee.UpdateTimeStamp = DateTime.UtcNow;
            employee.ProfilePicturePath = user.ProfilePicture;
            var x = await _employeeRepo.Update(employee);

            if (x > 0)
            {

            }

            return null;
        }
       
        private byte[] EncryptedPassword(string password)
        {
            using (RijndaelManaged myRijndael = new RijndaelManaged())
            {

                myRijndael.Key = Convert.FromBase64String(_configurationKey);
                myRijndael.IV = Convert.FromBase64String(_configurationVector);
                return EncryptStringToBytes(password, myRijndael.Key, myRijndael.IV);
            }
        }

        private string DecryptPassword(byte[] password)
        {
            using (RijndaelManaged myRijndael = new RijndaelManaged())
            {
                myRijndael.Key = Convert.FromBase64String(_configurationKey);
                myRijndael.IV = Convert.FromBase64String(_configurationVector);

                return DecryptStringFromBytes(password, myRijndael.Key, myRijndael.IV);
            }
        }
        static byte[] EncryptStringToBytes(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");
            byte[] encrypted;
            // Create an RijndaelManaged object
            // with the specified key and IV.
            using (RijndaelManaged rijAlg = new RijndaelManaged())
            {
                rijAlg.Key = Key;
                rijAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {

                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        static string DecryptStringFromBytes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an RijndaelManaged object
            // with the specified key and IV.
            using (RijndaelManaged rijAlg = new RijndaelManaged())
            {
                rijAlg.Key = Key;
                rijAlg.IV = IV;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }
    }
}
