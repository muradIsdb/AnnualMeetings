import React, { useState, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { transportApi } from '../../api/transport';
import { useAuth } from '../../hooks/useAuth';

interface TransportAssignVehicleModalProps {
  guestId: string;
  guestName: string;
  activeAssignment?: any;
  deservedCarClassId?: string;
  deservedCarClassName?: string;
  deservedCarClassColor?: string;
  onClose: () => void;
}

export const TransportAssignVehicleModal: React.FC<TransportAssignVehicleModalProps> = ({
  guestId,
  guestName,
  activeAssignment,
  deservedCarClassId,
  deservedCarClassName,
  deservedCarClassColor,
  onClose
}) => {
  const { user } = useAuth();
  const isAdminOrTransport = user?.roles?.some((r: string) => ['Admin', 'Transport'].includes(r)) ?? false;
  const queryClient = useQueryClient();

  // State matching the original qv/TAM function
  const [selectedVehicleId, setSelectedVehicleId] = useState('');
  const [assignmentType, setAssignmentType] = useState<'Dedicated' | 'Drop-off'>('Dedicated');
  const [notes, setNotes] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [forceReassign, setForceReassign] = useState(false);
  const [showAvailableOnly, setShowAvailableOnly] = useState(true);
  const [conflictingVehicle, setConflictingVehicle] = useState<any | null>(null);
  const [scannedQR, setScannedQR] = useState('');
  const [showQRScanner, setShowQRScanner] = useState(false);
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Fetch all vehicles with their status
  const { data: vehicles = [], isLoading } = useQuery({
    queryKey: ['vehicles-all-with-status'],
    queryFn: () => transportApi.getAllWithStatus(),
    enabled: true // Always true in TAM, since component only mounts when open
  });

  // Filter logic matching original bundle
  const availableVehicles = vehicles.filter((v: any) => v.status === 'Available' || (activeAssignment && v.id === activeAssignment.vehicleId));
  const baseList = showAvailableOnly ? availableVehicles : vehicles;
  
  // Apply car class filter if guest deserves a specific class
  const classFilteredList = deservedCarClassId 
    ? baseList.filter((v: any) => v.carClassId === deservedCarClassId) 
    : baseList;
    
  // The final list to display based on search
  const displayList = (showAvailableOnly ? baseList : classFilteredList).filter((v: any) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return (
      v.licensePlate.toLowerCase().includes(q) ||
      v.make.toLowerCase().includes(q) ||
      v.model.toLowerCase().includes(q) ||
      (v.color ?? '').toLowerCase().includes(q) ||
      (v.driverName ?? '').toLowerCase().includes(q) ||
      (v.currentGuestName ?? '').toLowerCase().includes(q) ||
      (v.carNumber ?? '').toLowerCase().includes(q)
    );
  });

  // Focus search input on mount
  useEffect(() => {
    setTimeout(() => {
      searchInputRef.current?.focus();
    }, 50);
  }, []);

  // Reset QR state when vehicle changes
  useEffect(() => {
    setScannedQR('');
    setConflictingVehicle(null);
  }, [selectedVehicleId]);

  // QR scan handler
  const handleQRScan = (scannedData: string) => {
    setShowQRScanner(false);
    const code = scannedData.trim();
    if (!code) return;

    // Try finding by car number
    const byCarNumber = vehicles.find((v: any) => v.carNumber?.trim().toLowerCase() === code.toLowerCase());
    if (byCarNumber) {
      setSelectedVehicleId(byCarNumber.id);
      setScannedQR(byCarNumber.carNumber ?? code);
      setSearchQuery(byCarNumber.carNumber ?? code);
      setShowAvailableOnly(true);
      
      if (byCarNumber.status === 'Assigned' && byCarNumber.currentGuestId && byCarNumber.currentGuestId !== guestId) {
        setConflictingVehicle(byCarNumber);
        setForceReassign(true);
      } else {
        setConflictingVehicle(null);
        if (byCarNumber.status !== 'Available') setForceReassign(true);
      }
      return;
    }

    // Try finding by license plate
    const byPlate = vehicles.find((v: any) => v.licensePlate.trim().toLowerCase() === code.toLowerCase());
    if (byPlate) {
      setSelectedVehicleId(byPlate.id);
      setSearchQuery(byPlate.licensePlate);
      setShowAvailableOnly(true);
      
      if (byPlate.status === 'Assigned' && byPlate.currentGuestId && byPlate.currentGuestId !== guestId) {
        setConflictingVehicle(byPlate);
        setForceReassign(true);
      } else {
        setConflictingVehicle(null);
        if (byPlate.status !== 'Available') setForceReassign(true);
      }
    }
  };

  // Assign mutation
  const assignMutation = useMutation({
    mutationFn: (data: any) => transportApi.assignVehicle(data),
    onSuccess: () => {
      // Invalidate relevant queries
      queryClient.invalidateQueries({ queryKey: ['guests/transport-queue'] });
      queryClient.invalidateQueries({ queryKey: ['guests/transport-in-transit'] });
      queryClient.invalidateQueries({ queryKey: ['guests/transport-all'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard/summary/transport'] });
      queryClient.invalidateQueries({ queryKey: ['vehicles-all-with-status'] });
      onClose();
    }
  });

  const handleAssign = () => {
    if (!selectedVehicleId) return;
    
    assignMutation.mutate({
      guestId,
      vehicleId: selectedVehicleId,
      assignmentType,
      notes,
      forceReassign
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex justify-between items-center">
          <h2 className="text-xl font-semibold text-gray-900">Assign Vehicle</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-500">
            ✕
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto flex-1">
          {/* Guest Info */}
          <div className="mb-4 p-3 bg-gray-50 rounded-lg">
            <p className="text-sm text-gray-500">Assigning to:</p>
            <p className="font-medium text-gray-900">{guestName}</p>
            {deservedCarClassName && (
              <p className="text-sm mt-1" style={{ color: deservedCarClassColor || '#666' }}>
                Deserves: {deservedCarClassName}
              </p>
            )}
          </div>

          {/* Search & QR */}
          <div className="flex space-x-2 mb-4">
            <div className="relative flex-1">
              <input
                ref={searchInputRef}
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search by plate, car #, make, model, driver..."
                className="w-full pl-3 pr-10 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-green-500"
              />
            </div>
            <button
              onClick={() => setShowQRScanner(true)}
              className="p-2 border border-gray-300 rounded-md hover:bg-gray-50 text-gray-600"
              title="Scan QR code to find vehicle"
            >
              📷
            </button>
          </div>

          {/* Filters */}
          <div className="flex items-center justify-between mb-4">
            <button
              onClick={() => setShowAvailableOnly(!showAvailableOnly)}
              className={`px-3 py-1 text-sm rounded-full ${
                showAvailableOnly ? 'bg-green-600 text-white' : 'bg-gray-200 text-gray-700'
              }`}
            >
              {showAvailableOnly ? '✓ Show available only' : 'Show all vehicles'}
            </button>
          </div>

          {/* Vehicle List */}
          <div className="border border-gray-200 rounded-md overflow-hidden mb-6 max-h-60 overflow-y-auto">
            {isLoading ? (
              <div className="p-4 text-center text-gray-500">Loading vehicles...</div>
            ) : displayList.length === 0 ? (
              <div className="p-4 text-center text-gray-500">No vehicles found</div>
            ) : (
              <ul className="divide-y divide-gray-200">
                {displayList.map((vehicle: any) => (
                  <li key={vehicle.id}>
                    <button
                      onClick={() => setSelectedVehicleId(vehicle.id)}
                      className={`w-full text-left p-3 hover:bg-gray-50 transition-colors ${
                        selectedVehicleId === vehicle.id ? 'bg-green-50 border-l-4 border-green-500' : ''
                      }`}
                    >
                      <div className="flex justify-between items-start">
                        <div>
                          <p className="font-medium text-gray-900">
                            {vehicle.make} {vehicle.model}
                            {vehicle.carNumber && <span className="ml-2 text-gray-500 text-sm">{vehicle.carNumber}</span>}
                          </p>
                          <p className="text-sm text-gray-500 mt-1">
                            {vehicle.driverName && <span className="mr-2">👤 {vehicle.driverName}</span>}
                            <span className="font-mono bg-gray-100 px-1 py-0.5 rounded">{vehicle.licensePlate}</span>
                          </p>
                        </div>
                        <span className={`px-2 py-1 text-xs rounded-full ${
                          vehicle.status === 'Available' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'
                        }`}>
                          {vehicle.status}
                        </span>
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Assignment Type */}
          <div className="mb-6">
            <label className="block text-sm font-medium text-gray-700 mb-2">Assignment Type</label>
            <div className="flex rounded-md shadow-sm">
              <button
                onClick={() => setAssignmentType('Drop-off')}
                className={`flex-1 py-2 text-sm font-medium border ${
                  assignmentType === 'Drop-off'
                    ? 'bg-green-50 border-green-500 text-green-700 z-10'
                    : 'bg-white border-gray-300 text-gray-700 hover:bg-gray-50'
                } rounded-l-md`}
              >
                Drop-off
              </button>
              <button
                onClick={() => setAssignmentType('Dedicated')}
                className={`flex-1 py-2 text-sm font-medium border-t border-b border-r ${
                  assignmentType === 'Dedicated'
                    ? 'bg-green-50 border-green-500 text-green-700 z-10'
                    : 'bg-white border-gray-300 text-gray-700 hover:bg-gray-50'
                } rounded-r-md`}
              >
                Dedicated
              </button>
            </div>
            <p className="mt-1 text-xs text-gray-500">
              {assignmentType === 'Drop-off' 
                ? 'Vehicle will return to pool after dropping guest' 
                : 'Vehicle is dedicated to this guest throughout their stay'}
            </p>
          </div>

          {/* Conflicting Vehicle Warning */}
          {conflictingVehicle && (
            <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-md">
              <p className="text-sm text-red-800 font-medium">⚠️ Vehicle is currently assigned to {conflictingVehicle.currentGuestName}</p>
              {isAdminOrTransport && (
                <label className="flex items-center mt-2">
                  <input
                    type="checkbox"
                    checked={forceReassign}
                    onChange={(e) => setForceReassign(e.target.checked)}
                    className="h-4 w-4 text-red-600 focus:ring-red-500 border-gray-300 rounded"
                  />
                  <span className="ml-2 text-sm text-red-700">Force reassign to {guestName}</span>
                </label>
              )}
            </div>
          )}

          {/* Notes */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notes (optional)</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={3}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-green-500"
              placeholder="Any special instructions..."
            />
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50 flex justify-end space-x-3">
          <button
            onClick={onClose}
            className="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            onClick={handleAssign}
            disabled={!selectedVehicleId || assignMutation.isPending || (conflictingVehicle && !forceReassign)}
            className={`px-4 py-2 rounded-md text-sm font-medium text-white ${
              !selectedVehicleId || assignMutation.isPending || (conflictingVehicle && !forceReassign)
                ? 'bg-green-400 cursor-not-allowed'
                : 'bg-green-600 hover:bg-green-700'
            }`}
          >
            {assignMutation.isPending ? 'Assigning...' : 'Assign Vehicle'}
          </button>
        </div>
      </div>
    </div>
  );
};
