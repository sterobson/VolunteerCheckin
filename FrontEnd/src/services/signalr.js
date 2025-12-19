import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from '../config';

let connection = null;

export const startSignalRConnection = async (onCheckinUpdate) => {
  if (connection) {
    return connection;
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/negotiate`, {
      skipNegotiation: false,
    })
    .withAutomaticReconnect()
    .build();

  connection.on('checkinUpdate', onCheckinUpdate);

  try {
    await connection.start();
    console.log('SignalR connected');
    return connection;
  } catch (error) {
    console.error('SignalR connection error:', error);
    return null;
  }
};

export const stopSignalRConnection = async () => {
  if (connection) {
    await connection.stop();
    connection = null;
  }
};

export const getConnection = () => connection;
