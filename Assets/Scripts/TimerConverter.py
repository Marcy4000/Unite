# Very stupid script for conversion from old timer system to the new one

def convert_times_to_more_than(times, max_time):
    """Convert times from less than condition to more than condition."""
    return [max_time - time for time in times]

def main():
    print("Welcome to the Time Converter!")
    while True:
        try:
            max_time = float(input("Enter the maximum time: "))
            times_input = input(
                "Enter the times to convert (comma-separated): "
            )
            times = list(map(float, times_input.split(',')))
            converted_times = convert_times_to_more_than(times, max_time)
            print("\nConverted times (for 'more than' condition):")
            print(", ".join(f"{t:.2f}" for t in converted_times))
        except ValueError:
            print("Invalid input. Please enter numeric values.")
        again = input("\nDo you want to convert more times? (yes/no): ").strip().lower()
        if again != "yes":
            break

if __name__ == "__main__":
    main()
