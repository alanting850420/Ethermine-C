using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace ethermine
{
    public partial class Form1 : Form
    {
        private int ID = 0;
        DataTable Miners = new DataTable();
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string query = "SELECT id, name FROM groups";
            try
            {
                MySqlDataAdapter riverSourcesAdapter = new MySqlDataAdapter(query, MySQLInfo.GetConnectionString());
                DataSet riverDataSet = new DataSet();
                riverSourcesAdapter.Fill(riverDataSet);
                group_cbx.DisplayMember = "name";
                group_cbx.ValueMember = "id";
                group_cbx.DataSource = riverDataSet.Tables[0];
            }
            catch
            {
                MessageBox.Show("資料庫沒開!");
                this.Close();
            }
            button1.PerformClick();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (timer1.Enabled)
            {
                timer1.Enabled = false;
                button1.Text = "啟動";
            }
            else
            {
                ID = 0;
                int.TryParse(group_cbx.SelectedValue.ToString(), out ID);
                if (ID != 0)
                {
                    Miners = new DataTable();
                    try
                    {
                        string SQL = "SELECT id, miner FROM miners WHERE id in (SELECT miner_id FROM group_miners WHERE group_id = " + ID + ")";
                        using (MySqlConnection conn = new MySqlConnection(MySQLInfo.GetConnectionString()))
                        {
                            conn.Open();
                            
                            using (MySqlDataAdapter returnVal = new MySqlDataAdapter(SQL, conn))
                            {
                                returnVal.Fill(Miners);
                            }
                        }
                        timer1.Enabled = true;
                        timer1_Tick(null, null);
                        button1.Text = "停止";
                    }
                    catch{}
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Miners = new DataTable();
            try
            {
                string SQL = "SELECT id, miner FROM miners WHERE id in (SELECT miner_id FROM group_miners WHERE group_id = " + ID + ")";
                using (MySqlConnection conn = new MySqlConnection(MySQLInfo.GetConnectionString()))
                {
                    conn.Open();

                    using (MySqlDataAdapter returnVal = new MySqlDataAdapter(SQL, conn))
                    {
                        returnVal.Fill(Miners);
                    }
                }
            }
            catch { }
            try
            {
                using (MySqlConnection conn = new MySqlConnection(MySQLInfo.GetConnectionString()))
                {
                    conn.Open();

                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = conn;
                        foreach (DataRow row in Miners.Rows)
                        {
                            var client = new RestClient("https://api.ethermine.org/miner/" + row[1].ToString() + "/workers");
                            var request = new RestRequest(Method.GET);
                            IRestResponse response = client.Execute(request);
                            var settings = new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                MissingMemberHandling = MissingMemberHandling.Ignore
                            };
                            EJson eJson = JsonConvert.DeserializeObject<EJson>(response.Content, settings);
                            Console.WriteLine(response.Content);
                            foreach (EData data in eJson.data)
                            {
                                
                                if (data.lastSeen != null)
                                {
                                    cmd.CommandText = "UPDATE workers SET updated_at = now(), lastSeen = ?lastSeen, reportedHashrate = ?reportedHashrate WHERE miner_id = " + row[0].ToString() + " AND worker = '" + data.worker + "'";
                                    cmd.Parameters.AddWithValue("?lastSeen", FromUnixEpoch(data.lastSeen));
                                    cmd.Parameters.AddWithValue("?reportedHashrate", data.reportedHashrate/1000000);
                                    if (cmd.ExecuteNonQuery() == 0)
                                    {
                                        cmd.CommandText = "INSERT INTO workers (miner_id, worker, lastSeen, reportedHashrate) VALUES (" + row[0].ToString() + ",'" + data.worker + "',?lastSeen, ?reportedHashrate)";
                                        cmd.ExecuteNonQuery();
                                    }
                                    cmd.Parameters.RemoveAt("?lastSeen");
                                    cmd.Parameters.RemoveAt("?reportedHashrate");
                                }
                            }
                        }
                    }

                }
            }
            catch { }
        }

        DateTime FromUnixEpoch(long? epochTime)
        {
            var epoch = new DateTime(1970, 1, 1, 8, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds((int)(epochTime));
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(MySQLInfo.GetConnectionString()))
                {
                    conn.Open();
                    int time = 15;
                    string SQL = "SELECT access_token, miner, worker, lastSeen, IFNULL(B.alias, '無別名'), IFNULL(A.alias, '無別名') FROM (SELECT miner_id, worker, lastSeen, alias FROM workers WHERE enable = 1 AND notices < date_sub(now(), interval '0:" + time + "' HOUR_MINUTE) AND lastSeen < date_sub(now(), interval '0:31' HOUR_MINUTE)) A LEFT JOIN (SELECT miners.id miner_id, miner, access_token, alias FROM miners LEFT JOIN users ON miners.user_id = users.id) B ON A.miner_id = B.miner_id";
                    DataTable dt = new DataTable();
                    using (MySqlDataAdapter returnVal = new MySqlDataAdapter(SQL, conn))
                    {
                        returnVal.Fill(dt);
                    }
                    foreach (DataRow row in dt.Rows)
                    {
                        var client = new RestClient("https://notify-api.line.me/api/notify");
                        var request = new RestRequest(Method.POST);
                        request.AddHeader("authorization", "Bearer " + row[0].ToString());
                        request.AddHeader("content-type", "multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW");
                        request.AddParameter("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW", "------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"message\"\r\n\r\n\r\n異常斷線\r\nMiner=" + row[1].ToString() + "(" + row[4].ToString() + ")\r\nWorker=" + row[2].ToString() + "(" + row[5].ToString() + ")\r\n最後出現時間=" + row[3].ToString() + "\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW--", ParameterType.RequestBody);
                        IRestResponse response = client.Execute(request);
                    }

                    SQL = "SELECT access_token, miner, worker, IFNULL(reportedHashrate, 0), IFNULL(targetHashrate, 0), IFNULL(B.alias, '無別名'), IFNULL(A.alias, '無別名') FROM (SELECT miner_id, worker, reportedHashrate, targetHashrate, alias FROM workers WHERE enable = 1 AND notices < date_sub(now(), interval '0:" + time + "' HOUR_MINUTE) AND IFNULL(reportedHashrate, 0) < IFNULL(targetHashrate, 0) - 20) A LEFT JOIN (SELECT miners.id miner_id, miner, access_token, alias FROM miners LEFT JOIN users ON miners.user_id = users.id) B ON A.miner_id = B.miner_id";
                    dt = new DataTable();
                    using (MySqlDataAdapter returnVal = new MySqlDataAdapter(SQL, conn))
                    {
                        returnVal.Fill(dt);
                    }
                    foreach (DataRow row in dt.Rows)
                    {
                        var client = new RestClient("https://notify-api.line.me/api/notify");
                        var request = new RestRequest(Method.POST);
                        request.AddHeader("authorization", "Bearer " + row[0].ToString());
                        request.AddHeader("content-type", "multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW");
                        request.AddParameter("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW", "------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"message\"\r\n\r\n\r\n算力不足\r\nMiner=" + row[1].ToString() + "(" + row[5].ToString() + ")\r\nWorker=" + row[2].ToString() + "(" + row[6].ToString() + ")\r\n實際算力=" + row[3].ToString() + "\r\n目標算力=" + row[4].ToString() + "\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW--", ParameterType.RequestBody);
                        IRestResponse response = client.Execute(request);
                    }
                    SQL = "UPDATE workers SET notices = now() WHERE enable = 1 AND notices < date_sub(now(), interval '0:" + time + "' HOUR_MINUTE) AND (IFNULL(reportedHashrate, 0) < IFNULL(targetHashrate, 0) - 20 OR lastSeen < date_sub(now(), interval '0:20' HOUR_MINUTE))";

                    using (MySqlCommand cmd = new MySqlCommand(SQL,conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }
    }
}
